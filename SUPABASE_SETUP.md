# Supabase setup

1. Run `Supabase/schema.sql` in the Supabase SQL editor.
2. Copy your project URL and publishable anon key into `Assets/Resources/SupabaseConfig.json`.
3. Do not put a service role key or secret key in Unity.

This project uses normal Supabase tables and RPC functions only:

- `app_players` stores name, phone, optional email, a hashed password, and a hashed session token.
- `leaderboard_scores` stores each player's best score.
- `leaderboard` is the public read view used by the Unity leaderboard UI.
- `register_player`, `sign_in_player`, and `submit_score` are called by Unity using the publishable anon key.

Passwords are not stored as plain text. The database hashes passwords with `pgcrypto`. The Unity client stores only the generated player id and session token locally so scores can be submitted after registration/sign-in.
