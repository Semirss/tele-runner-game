# Unity Setup Checklist

Use this checklist after opening the project in Unity. Do these steps in order.

## 1. Package resolver

- Close the old MCP Setup window if it is still open.
- Restart Unity after pulling these changes.
- Unity MCP was removed from `Packages/manifest.json`, so you do not need to install `uv` for this project.
- If Unity says `manifest.json is not valid JSON` again, close Unity and make sure the file is saved as UTF-8 without BOM.

## 2. Supabase backend

This project uses Supabase tables and RPC functions only. Do not enable or use Supabase Auth.

1. Open your Supabase project.
2. Open SQL Editor.
3. Run the SQL file from this project:
   - `Supabase/schema.sql`
4. Confirm these exist in Supabase:
   - table: `app_players`
   - table: `leaderboard_scores`
   - view: `leaderboard`
   - RPC: `register_player`
   - RPC: `sign_in_player`
   - RPC: `submit_score`
5. Check `Assets/Resources/SupabaseConfig.json`:
   - `projectUrl` must be your Supabase project URL.
   - `publishableKey` must be your Supabase anon/publishable key.
   - `leaderboardLimit` can stay `50`.

Important: phone and password are stored through the table/RPC flow. The phone field accepts whatever the player types; it is not restricted by Unity.

## 3. Start scene registration/sign-in

The start scene now shows the registration/sign-in UI automatically when no local Supabase player session exists.

Prepare this:

- Scene name must stay `Start` unless you also update `SupabaseBootstrap`.
- Make sure `Assets/Resources/SupabaseConfig.json` is configured before testing.
- Register flow asks for:
  - name
  - phone
  - email, optional
  - password
- Sign-in flow asks for:
  - phone
  - password

Test:

1. Clear PlayerPrefs if you want to test first-time registration again.
2. Enter a new player.
3. Confirm a row appears in Supabase `app_players`.
4. Close/reopen play mode and confirm sign-in/session still works.

## 4. Leaderboard UI

The leaderboard now reads real Supabase rows from the `leaderboard` view. It should not show sample/local data.

Prepare your row prefab:

- Add/keep `HighscoreUI` on the row prefab.
- Assign its text fields:
  - `number` = rank text
  - `playerName` = player name text
  - `score` = score text
- Assign this prefab to `Leaderboard.rowPrefab`.
- Assign the parent transform to `Leaderboard.entriesRoot`.

Test:

1. Register/sign in.
2. Finish a run with a score.
3. Confirm `leaderboard_scores` updates in Supabase.
4. Open leaderboard and confirm real names/scores/ranks appear.

## 5. Loadout top name and rank

In the Main scene, select the object with `LoadoutState`.

Assign these inspector fields:

- `playerNameDisplay` = the Text at the top where the player name should appear.
- `playerRankDisplay` = the Text at the top where rank should appear.

Expected display:

- Name comes from Supabase local player data.
- Rank displays as `Rank X` from `PlayerData.rank`.

## 6. Shop

The shop should only use the Items section now.

Prepare this:

- Do not use accessory sections.
- Do not use theme sections.
- Only add consumable/item prefabs to the item database/list.
- Add the new bike lane powerup to the consumable database after you create its prefab.

## 7. Bike lane powerup

Code added:

- `Assets/Scripts/Consumable/Types/BikeLanePowerup.cs`
- `ConsumableType.BIKE_LANE`
- bike lane support in `TrackManager`
- forced-lane support in `CharacterInputController`

### TrackManager setup

In the Main scene, select `TrackManager` and set:

- `Bike Lane Index`
  - `0` = left
  - `1` = middle
  - `2` = right
- `Bike Lane Speed Multiplier`
  - recommended start: `1.65`

### Bike powerup prefab setup

Create it like this:

1. Duplicate an existing powerup prefab.
2. Add or replace the script with `BikeLanePowerup`.
3. Set `duration`.
4. Keep `targetLane = -1` if you want to use `TrackManager.Bike Lane Index`.
5. Keep `speedMultiplier = 0` if you want to use `TrackManager.Bike Lane Speed Multiplier`.
6. Assign `icon`, sound, and particle references if needed.
7. Assign your bike/rider prefab to `bikePrefab`.
8. Adjust:
   - `bikeLocalPosition`
   - `bikeLocalEulerAngles`
   - `bikeLocalScale`
9. Keep `hideCharacterModelWhileRiding` enabled if your bike prefab includes the rider model.
10. Make the prefab Addressable the same way existing powerups are Addressable.
11. Add it to `ConsumableDatabase.consumbales`.

Expected behavior:

- Player is forced into the bike lane while active.
- Speed is boosted while active.
- The normal runner model can be hidden while the bike is shown.
- Newly spawned segments do not spawn obstacles while bike mode is active.
- Coins spawn in the bike lane.
- Already-spawned obstacles are not deleted, so temporary invincibility is applied when the powerup starts.

## 8. Bus obstacle / bus roof riding

Code added:

- `Assets/Scripts/Obstacles/BusObstacle.cs`
- `Assets/Scripts/Obstacles/BusRideSurface.cs`
- ride-surface support in `CharacterInputController`

### Bus prefab setup

Create a bus prefab like this:

1. Add `BusObstacle` to the bus root.
2. Set `laneIndex`:
   - `-1` = random lane
   - `0` = left
   - `1` = middle
   - `2` = right
3. Add body colliders for the bus obstacle.
4. Put body colliders on the `Obstacle` layer.
5. Add a child object for the roof trigger.
6. Add a trigger collider to the roof child.
7. Add `BusRideSurface` to the roof child.
8. Keep the roof trigger off the `Obstacle` layer so landing on the roof does not count as a crash.
9. Tune:
   - `rideHeight`
   - `rideDistance`
   - `endOnExit`, usually keep this off for fast movement
10. Make the bus prefab Addressable like other obstacles.
11. Add the bus prefab to the `possibleObstacles` list on your track segments.

Test:

- Jump onto the roof trigger.
- The player should ride at the configured height for the configured distance.
- If the player hits the obstacle body instead, it should still behave like an obstacle.


## Portrait-only orientation

The project is configured for portrait only.

Project setting changed:

- `ProjectSettings/ProjectSettings.asset`
  - `defaultScreenOrientation: 0`
  - `allowedAutorotateToPortrait: 1`
  - `allowedAutorotateToPortraitUpsideDown: 0`
  - `allowedAutorotateToLandscapeRight: 0`
  - `allowedAutorotateToLandscapeLeft: 0`

Runtime guard added:

- `Assets/Scripts/System/PortraitModeEnforcer.cs`

This script runs before scenes load and forces:

- portrait enabled
- upside-down portrait disabled
- both landscape directions disabled
- `Screen.orientation = ScreenOrientation.Portrait`

For Android/iOS, this should prevent landscape rotation. For WebGL, browser/device orientation locking can depend on the browser and fullscreen permissions, but the Unity project and runtime are both set to portrait.

### Mesh collider bus setup

You can use a `MeshCollider` for the bus. You do not need a separate box/body collider.

Required setup:

1. Put `BusObstacle` on the bus root object or on a parent of the mesh collider.
2. Keep `rideableOnContact` enabled on `BusObstacle`.
3. Put the bus mesh collider object on the `Obstacle` layer.
4. `useColliderTopAsRideHeight` should stay enabled so the ride height is calculated from the mesh collider bounds.
5. Tune `rideHeightOffset` if the runner clips into the bus roof.
6. Tune `rideDistance` so the player stays on the bus long enough to cross it.
7. Tune `CharacterInputController.rideSurfaceVerticalSpeed` if the climb onto the bus feels too slow or too fast.

When a player runs into a rideable bus collider, the collision is consumed as a ride start, not as damage. Regular obstacles without `BusObstacle` or `BusRideSurface` still damage the player.
## 9. WebGL build checklist

Before final WebGL build:

- Let Unity finish importing all scripts.
- Fix any red Console errors before building.
- Build Addressables if your workflow requires it.
- Use Brotli compression if your host supports it.
- Keep Decompression Fallback off only if your host sends correct compression headers.
- Test the final build from a local/server host, not by opening `index.html` directly.

## 10. Quick final test order

1. Unity opens with no package resolver errors.
2. Start scene opens.
3. Register a new player.
4. Run the game.
5. Finish a run and submit score.
6. Leaderboard shows Supabase rows only.
7. Loadout shows player name and rank.
8. Bike powerup appears and activates.
9. Bike lane has coins and no newly spawned obstacles.
10. Bus obstacles spawn.
11. Player can ride on bus roof trigger.
12. WebGL build completes.