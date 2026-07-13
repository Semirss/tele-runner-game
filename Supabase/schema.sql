create schema if not exists extensions;
create extension if not exists pgcrypto with schema extensions;

-- Cleanup from earlier backend schema versions.
drop view if exists public.leaderboard;
drop function if exists public.submit_score(bigint);
drop function if exists public.submit_score(uuid, text, bigint);
drop function if exists public.sign_in_player(text, text);
drop function if exists public.register_player(text, text, text, text);
drop function if exists public.handle_new_user();
drop table if exists public.leaderboard_scores;
drop table if exists public.profiles;

create table if not exists public.app_players (
  id uuid primary key default gen_random_uuid(),
  display_name text not null,
  phone text not null unique,
  email text,
  password_hash text not null,
  session_token_hash text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.leaderboard_scores (
  app_player_id uuid primary key references public.app_players(id) on delete cascade,
  player_name text not null,
  score bigint not null default 0,
  updated_at timestamptz not null default now()
);

alter table public.app_players enable row level security;
alter table public.leaderboard_scores enable row level security;

create or replace function public.register_player(
  p_display_name text,
  p_phone text,
  p_email text,
  p_password text
)
returns table (
  id uuid,
  display_name text,
  phone text,
  email text,
  session_token text
)
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_display_name text := coalesce(nullif(btrim(p_display_name), ''), 'Player');
  v_phone text := btrim(p_phone);
  v_email text := nullif(btrim(coalesce(p_email, '')), '');
  v_session_token text := encode(gen_random_bytes(32), 'hex');
  v_player public.app_players%rowtype;
begin
  if v_phone is null or v_phone = '' then
    raise exception 'Phone number is required.';
  end if;

  if p_password is null or p_password = '' then
    raise exception 'Password is required.';
  end if;

  if exists (select 1 from public.app_players existing where existing.phone = v_phone) then
    raise exception 'A player with this phone already exists. Sign in instead.';
  end if;

  insert into public.app_players (display_name, phone, email, password_hash, session_token_hash)
  values (
    v_display_name,
    v_phone,
    v_email,
    crypt(p_password, gen_salt('bf', 10)),
    crypt(v_session_token, gen_salt('bf', 10))
  )
  returning * into v_player;

  return query select v_player.id, v_player.display_name, v_player.phone, v_player.email, v_session_token;
end;
$$;

create or replace function public.sign_in_player(
  p_phone text,
  p_password text
)
returns table (
  id uuid,
  display_name text,
  phone text,
  email text,
  session_token text
)
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_phone text := btrim(p_phone);
  v_session_token text := encode(gen_random_bytes(32), 'hex');
  v_player public.app_players%rowtype;
begin
  select *
    into v_player
  from public.app_players player
  where player.phone = v_phone
    and player.password_hash = crypt(p_password, player.password_hash);

  if v_player.id is null then
    raise exception 'Invalid phone or password.';
  end if;

  update public.app_players
  set session_token_hash = crypt(v_session_token, gen_salt('bf', 10)),
      updated_at = now()
  where app_players.id = v_player.id
  returning * into v_player;

  return query select v_player.id, v_player.display_name, v_player.phone, v_player.email, v_session_token;
end;
$$;

create or replace function public.submit_score(
  p_app_player_id uuid,
  p_session_token text,
  p_score bigint
)
returns void
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
  v_player public.app_players%rowtype;
begin
  select *
    into v_player
  from public.app_players player
  where player.id = p_app_player_id
    and player.session_token_hash = crypt(p_session_token, player.session_token_hash);

  if v_player.id is null then
    raise exception 'Invalid player session. Sign in again.';
  end if;

  insert into public.leaderboard_scores (app_player_id, player_name, score, updated_at)
  values (v_player.id, coalesce(nullif(v_player.display_name, ''), v_player.phone, 'Player'), greatest(p_score, 0), now())
  on conflict (app_player_id) do update set
    player_name = excluded.player_name,
    score = greatest(public.leaderboard_scores.score, excluded.score),
    updated_at = now();
end;
$$;

create or replace view public.leaderboard as
select
  dense_rank() over (order by score desc, updated_at asc) as rank,
  player_name,
  score
from public.leaderboard_scores;

revoke all on public.app_players from anon;
revoke all on public.leaderboard_scores from anon;
grant select on public.leaderboard to anon;
grant execute on function public.register_player(text, text, text, text) to anon;
grant execute on function public.sign_in_player(text, text) to anon;
grant execute on function public.submit_score(uuid, text, bigint) to anon;


