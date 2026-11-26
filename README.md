# 2D Procedural Animation Learning

Prototype pengembangan animasi prosedural 2D di Unity, mencakup sistem kamera yang di-polish, gerak platformer berbasis fisika, animasi prosedural tubuh (tungkai, lengan, bahu), sistem damage dummy, dan weapon combat sederhana.

**Engine:** Unity 6 (6000.3.9f1)  
**Genre:** 2D Platformer (Prototype)  
**Product Name:** 2DProceduralAnimation

---

## Fitur Utama

### Kamera (`Assets/_Scripts/Player_Scripts/Camera/`)
- `CameraFollow2D.cs` — Smooth follow ke player dengan `SmoothDamp`
- **Camera Shake** — Getaran layar saat serangan mengenai sasaran, dengan intensitas dan durasi yang bisa disesuaikan
- **Zoom Punch** — Zoom in mikro saat pukulan normal, dramatis saat critical
- **Cursor Look-Ahead** — Kamera mengikuti arah kursor mouse dengan batasan (deadzone + clamp max offset), dihitung via viewport space agar tidak terjadi feedback loop
- **Velocity Look-Ahead** — Kamera mendahului arah gerak player (antisipasi lompat / jatuh)
- **Vertical Damping** — Mengurangi goyang vertikal saat player di tanah
- **Level Bounds** — Kamera tidak keluar dari batas level (menggunakan `Collider2D` bounds)
- `FlipToMousePos.cs` — Flip sprite player ke arah kursor dengan deadzone

### Animasi Prosedural Player (`Assets/_Scripts/Player_Scripts/Procedural/Rebuild/`)
- `PlatformerMovement.cs` — Gerak horizontal, lompat, fall multiplier, wall slide dasar
- `ProceduralBipedLeg2DRebuild.cs` — Animasi tungkai prosedural (IK sederhana / procedural walk)
- `ProceduralArm2DRebuild.cs` & `ProceduralUpperBodyAim2D.cs` — Lengan dan tubuh atas ikut mengarah ke mouse (aim tracking)
- `PelvisController2DRebuild.cs` — Pengaturan pelvis saat bergerak

### Combat & Weapon
- `WeaponLogic.cs` / `MeleeLogic.cs` / `GunLogic.cs` — Logika serangan senjata
- `BaseWeaponSO.cs`, `MeleeWeaponSO.cs`, `GunWeaponSO.cs` — Data senjata via ScriptableObject
- `BaseEntitySO.cs` — Data entitas (darah, nama, immortal)
- `MeleeTrailVFXController.cs`, `CombatVFXController.cs`, `GhostSlashArc.cs` — VFX serangan

### Dummy / Target Latihan (`Assets/_Scripts/Enemy_Scripts/`)
- `DummyEntity.cs` — Entitas dummy yang bisa menerima damage, knockback, hit flash, squash & stretch, dan mati
- `DummyAnimationController.cs` — Pengatur state animasi dummy (Idle → Hit → Idle, Idle → Death)
- `WallCollisionReaction2D.cs` — Reaksi saat menabrak dinding (rebound + squash visual)

### UI & Audio
- `DamagePopUp2D.cs` — Angka damage yang melayang di world space
- `AudioManager.cs` — Manajemen audio via prefab
- `WeaponEquipmentManager.cs` — Pengelolaan senjata yang dipakai player

---

## Struktur Folder

```
Assets/
├── _Scripts/
│   ├── Enemy_Scripts/          # DummyEntity, DummyAnimationController
│   ├── Player_Scripts/
│   │   ├── Camera/             # CameraFollow2D, FlipToMousePos
│   │   ├── Procedural/Rebuild/ # Animasi prosedural (kaki, lengan, pelvis)
│   │   ├── Combat/             # WallCollisionReaction2D
│   │   ├── Weapon/             # WeaponLogic, MeleeLogic, GunLogic, projectile
│   │   ├── Weapon_SO/          # ScriptableObject senjata & entitas
│   │   └── Player/             # Input, Animator, AimTracker, controller tangan
│   ├── Manager/                # GameManager, WeaponEquipmentManager
│   ├── VFX_Scripts/            # Hit VFX, melee trail, ghost slash
│   ├── UI_Scripts/WorldSpace/  # DamagePopUp2D
│   ├── Audio/                  # AudioManager
│   ├── Interface/              # IDamageable
│   └── Enum/                   # WeaponTriggerMode, GunSystemState
├── _Prefabs/                   # Dummy, Weapon, VFX, AudioManager, DamagePop
├── Scenes/                     # SampleScene, New Scene
├── War/
│   └── Straw Training Dummy - Pixel Art/  # Asset dummy (sprite + animasi)
└── Animations/
    └── Player_Animations/      # Animator controller player
```

---

## Setup Awal di Unity

1. Buka project di Unity 6 (6000.3.9f1+).
2. Buka scene `Assets/Scenes/SampleScene.unity`.
3. Pastikan **Main Camera** memiliki komponen `CameraFollow2D`.
4. Assign `LevelBounds` (GameObject dengan `Collider2D` yang menutupi area main) ke field `_boundsCollider` di `CameraFollow2D`.
5. Prefab `Dummy` di `Assets/_Prefabs/Dummy.prefab` sudah terpasang:
   - `DummyEntity` (root)
   - `DummyAnimationController` + `Animator` (child `Visual`)
6. Tekan **Play** untuk mencoba:
   - Gerak player → kamera ikut + look-ahead
   - Serang dummy → animasi Hit + shake + zoom punch
   - Darah habis → animasi Death + hancur

---

## Parameter Kamera yang Disarankan

| Parameter | Default | Keterangan |
|-----------|---------|------------|
| `_lookAheadStrength` | 1.0 | Pengali look-ahead kursor |
| `_verticalMultiplier` | 0.45 | Semakin kecil, semakin stabil vertikal |
| `_lookDeadzone` | 0.12 | Radius tengah layar yang tidak goyang |
| `_maxLookOffset` | (4, 2.2) | Batas maksimal offset kursor (world unit) |
| `_velocityLookAhead` | 0.10 | Antisipasi arah gerak |
| `_groundedVerticalDamp` | 0.85 | Redam goyang vertikal saat di tanah |
| `_clampToBounds` | true | Aktifkan batas level |

---

## Flow Animator Dummy

Controller: `Assets/War/Straw Training Dummy - Pixel Art/Animations/Straw Training Dummy Animator Controller.controller`

**Parameter:**

| Nama | Tipe |
|------|------|
| `Hit` | Trigger |
| `Dead` | Trigger |

**State & Transisi:**

```
[Idle]  <--(Exit Time 0.9)--  [Hit]
   \                            ^
    \--(Dead trigger)--> [Death]|
                             ^  |
        AnyState --(Hit)-----|  |
        AnyState --(Dead)-------+
```

- `AnyState → Hit` : saat trigger `Hit` (instan)
- `AnyState → Death` : saat trigger `Dead` (instan)
- `Hit → Idle` : otomatis setelah animasi Hit selesai
- `Death` : menahan frame terakhir, objek dihancurkan setelah `_deathDestroyDelay` (default 1.6s)

---

## Cara Kontribusi / Pengembangan

- Buat branch baru sebelum modifikasi besar.
- Simpan ScriptableObject senjata di folder `Weapon_SO` dan reference di `WeaponEquipmentManager`.
- Jika menambah animasi dummy baru, tambahkan state & parameter di `Straw Training Dummy Animator Controller.controller` dan method di `DummyAnimationController.cs`.

---

## Lisensi

Prototype pembelajaran — bebas digunakan untuk belajar dan dikembangkan lebih lanjut.

---

*Terakhir diperbarui: 26 November 2026*
