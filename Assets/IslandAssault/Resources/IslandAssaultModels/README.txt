ISLAND ASSAULT - CUSTOM 3D MODELS
=================================
Drop 3D models (.fbx is best, or prefabs) into THIS folder and rename them to one of
the names below. The game will use them instead of the built-in shapes. Delete a file
to go back to the built-in look.

BUILDINGS                 OTHER
  HQ                        Palm          (palm trees)
  GoldMine                  Rock          (beach rocks)
  Sawmill                   LandingBoat   (troop boats)
  Barracks  (Landing Craft) Gunboat       (your big ship)
  Cannon                    Rifleman
  MachineGun                Heavy
  Sniper                    Rocketeer
  Mortar

Optional variations (most specific wins):
  Cannon_3        level 3 and up uses this one (until a higher number exists)
  Cannon_Enemy    only on enemy islands
  Cannon_3_Enemy  level 3+ on enemy islands

Tips
- Models are auto-scaled to fit their spot and placed on the ground.
- They should face +Z (blue arrow). If one faces the wrong way, make a prefab of it,
  rotate the child inside the prefab, and name the PREFAB "Cannon" etc.
- Defenses: a child object named with "Turret" / "Weapon" / "Head" / "Barrel" / "Gun"
  is the part that aims. Otherwise the whole model turns. A child named "Muzzle" is
  where shots come out.
- Textures: also copy the pack's texture (e.g. colormap.png) into your project.
  If a model looks white/grey: select it > Inspector > Materials > "Extract Textures"
  / "Extract Materials", or drag the texture onto the material's Base Map.
