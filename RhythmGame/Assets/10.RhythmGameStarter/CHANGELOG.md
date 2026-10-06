# Changelog

All notable changes to this project will be documented in this file. See [standard-version](https://github.com/conventional-changelog/standard-version) for commit guidelines.

## [0.11.0](https://gitlab.com/BennyKok/rhythm-core/compare/v0.10.4...v0.11.0) (2023-06-04)


### Features

* add combo break event in ComboSystem ([732245e](https://gitlab.com/BennyKok/rhythm-core/commit/732245e380b34b6b38765c610b9b7f5d0539059d))
* add cover art field in SongItem, SongListItem ([1d7ed49](https://gitlab.com/BennyKok/rhythm-core/commit/1d7ed490ff3b6ab79b8135f3ecc0486335e0460f))
* add horizontal recorder demo ([338c317](https://gitlab.com/BennyKok/rhythm-core/commit/338c317a34e2dab4ec08d29a4e0271ea310f0585))
* add last word option in count down ([b8c3c8a](https://gitlab.com/BennyKok/rhythm-core/commit/b8c3c8a14cda544944d2976657cee77d59d3d496))
* add pauseGameWhenVisible in view ([0b023b1](https://gitlab.com/BennyKok/rhythm-core/commit/0b023b176691b6d6f3d6af35390fadd3a2732206))
* add speed modifier in SongItem ([c86464e](https://gitlab.com/BennyKok/rhythm-core/commit/c86464ea4a3edbcc0280c9c7a0effc5531e326e9))
* note name display + generate midi track mapping from detected notes ([51b3324](https://gitlab.com/BennyKok/rhythm-core/commit/51b33245f3ee8b8b3aff126e94b143209a13683c))
* per SongItem prefab mapping and midi mapping override ([0442943](https://gitlab.com/BennyKok/rhythm-core/commit/0442943f03b197007b89244a59677a92a5330c1e))


### Bug Fixes

* ensure count down is stopped when the component is disabled ([3dd9900](https://gitlab.com/BennyKok/rhythm-core/commit/3dd9900f4cac3a13a9edef3b2943d315702c9abe))
* typo of useGenerator in SongItem ([5951360](https://gitlab.com/BennyKok/rhythm-core/commit/5951360ba4c7c262b2bde28abe3115c51ec0506c))
* update generated midi track mapping file name ([bef5715](https://gitlab.com/BennyKok/rhythm-core/commit/bef57154d0079b06cacaace77e5e469eccfc71d9))

### [0.10.4](https://gitlab.com/BennyKok/rhythm-core/compare/v0.10.3...v0.10.4) (2023-01-25)


### Features

* **editor:** add 12 note display in sequence editor ([63bcdb2](https://gitlab.com/BennyKok/rhythm-core/commit/63bcdb27748acdfb8dc0126b19ace0d778c00462))


### Bug Fixes

* all notes data is visible in debug mode in the SongItem's inspector ([3dbba59](https://gitlab.com/BennyKok/rhythm-core/commit/3dbba59ac9e2e28fdcc7d48d520d6b091af20249))
* **editor:** comment and title gui display on 2022.2 + ([001c358](https://gitlab.com/BennyKok/rhythm-core/commit/001c3585ae6ab6d3fc270b4c0687f7f6f706ce1a))

### [0.10.3](https://gitlab.com/BennyKok/rhythm-core/compare/v0.10.2...v0.10.3) (2022-06-04)


### Bug Fixes

* properly reset stats when song start ([6bf49d2](https://gitlab.com/BennyKok/rhythm-core/commit/6bf49d2145289897cf03d490dca0be1713fa8709))

### [0.10.2](https://gitlab.com/BennyKok/rhythm-core/compare/v0.10.1...v0.10.2) (2022-02-13)


### Features

* add an options in NoteArea to emit tap effects on custom transform ([ceeed23](https://gitlab.com/BennyKok/rhythm-core/commit/ceeed23f70efbcd091791a4f0ca0fc67d9a2de46))

### [0.10.1](https://gitlab.com/BennyKok/rhythm-core/compare/v0.10.0...v0.10.1) (2021-09-05)


### Features

* a new demo with song selection list + simple UI fade transition system ([e758085](https://gitlab.com/BennyKok/rhythm-core/commit/e75808569ab341e0c3bce1646d843e8cb00b05ff))
* added metadata in SongItem ([7979c83](https://gitlab.com/BennyKok/rhythm-core/commit/7979c83a8d416f8813054b2157f6d2a3e88981ce))

## [0.10.0](https://gitlab.com/BennyKok/rhythm-core/compare/v0.8.1...v0.10.0) (2021-07-26)


### Features

* **demo-selection:** updated demo selection scene, with a back button to reselect other demo ([7d25ce6](https://gitlab.com/BennyKok/rhythm-core/commit/7d25ce686d4184986c8e33555ff0243909742e29))
* added OnNoteTouchDown, OnNoteTouchUp in LongNoteDetecter, useful for long note effect per prefab ([6d626c4](https://gitlab.com/BennyKok/rhythm-core/commit/6d626c4ea48b08180a000321f113ac68f2506c77))
* simple trigger based input demo, useful for vr interaction ([0f7f06a](https://gitlab.com/BennyKok/rhythm-core/commit/0f7f06abd7535f1399f7a61c7ad469abbfc786af))


### Bug Fixes

* **demo:** fix orientation for the demo on iOS platform. ([5002eeb](https://gitlab.com/BennyKok/rhythm-core/commit/5002eeb133b3d8bd9ae4b916b94eb8872e8cb4a5))
* effects will follows correct scale, if the rgs core is being scaled ([ef2f00d](https://gitlab.com/BennyKok/rhythm-core/commit/ef2f00d97b174c03d302fe360f84da87c4df6ba4))
* rename tag variable in TriggerState.cs to colliderTag ([25e3a4e](https://gitlab.com/BennyKok/rhythm-core/commit/25e3a4e23ea10498779db4019ec480461c7d7170))
* reset combo when song start ([0913be3](https://gitlab.com/BennyKok/rhythm-core/commit/0913be3149950da19798fe4dfe93ebb349f171c6))
* **colorful-demo:** resetting labels and display on song start ([bb6311f](https://gitlab.com/BennyKok/rhythm-core/commit/bb6311faedb8d406f1080fb69615890e61f896a1))
* missing PlaySong(songItem) method for UnityEvent ([5e29c20](https://gitlab.com/BennyKok/rhythm-core/commit/5e29c2022ef8830c470d6f91462c87183ebf4d4b))
* scaling on entire track parent, individual track will works out of the box ([c62826b](https://gitlab.com/BennyKok/rhythm-core/commit/c62826bfe860b106d9e7beca9631ee9039e0f15a))
* ScreenOrientation enum update ([be39246](https://gitlab.com/BennyKok/rhythm-core/commit/be3924644434da64e5503850ed8dcb88d87c3c1c))

## [0.9.0](https://gitlab.com/BennyKok/rhythm-core/compare/v0.8.1...v0.9.0) (2021-05-28)


### ⚠ BREAKING CHANGES

* support for InputSystem's EnhancedTouch

### Features

* support for InputSystem's EnhancedTouch ([a9bbba1](https://gitlab.com/BennyKok/rhythm-core/commit/a9bbba1fc78598964b4229e4a016737fe617ee69))
* support for InputSystem's PlayerInput & InputActionAsset ([a5313aa](https://gitlab.com/BennyKok/rhythm-core/commit/a5313aaadf1e1c00feb1053cf2bf3ff22b63b6ee))


### Bug Fixes

* **editor:** label display problem in Recorder script ([8feb77c](https://gitlab.com/BennyKok/rhythm-core/commit/8feb77c64b91721ed25abca05a0a8cf50bbdab76))
* reset score when the song begins play ([7e0e7ac](https://gitlab.com/BennyKok/rhythm-core/commit/7e0e7ac1456bedf954f40e97e73e348b9bfa2326))
* reset stats property when restart ([ad11e73](https://gitlab.com/BennyKok/rhythm-core/commit/ad11e7333a90b12659a8b68552385b5ba76499a5))

### [0.8.1](https://gitlab.com/BennyKok/rhythm-core/compare/v0.8.0...v0.8.1) (2020-12-26)


### Features

* able to change sequencer item limit in editor ([dbb9f8a](https://gitlab.com/BennyKok/rhythm-core/commit/dbb9f8afb9864714049ea3ac525b59aef7e688c1))


### Bug Fixes

* **demo:** description label / play button order ([1badecb](https://gitlab.com/BennyKok/rhythm-core/commit/1badecb84e08e3d3aedfb2e78c87633529139823))
* **editor:** tweak title color in editor light mode ([97d8d9a](https://gitlab.com/BennyKok/rhythm-core/commit/97d8d9abb14e3f97e99b289a8f0eb2514ddd3d6a))

## [0.8.0](https://gitlab.com/BennyKok/rhythm-core/compare/v0.7.0...v0.8.0) (2020-10-20)


### Features

* **rhythm-core:** option to PlaySong with a specificStartTime ([07e3c69](https://gitlab.com/BennyKok/rhythm-core/commit/07e3c692cb81b74d1cd8b105cc285dfd7923f0ec))
* **editor:** color highlighted title for core component inspector ([a9f788a](https://gitlab.com/BennyKok/rhythm-core/commit/a9f788ac39b0e7b2997bc8a8ea146afc38c4e51c))
* **editor:** new responsive foldout update for all editor ([e7caa10](https://gitlab.com/BennyKok/rhythm-core/commit/e7caa10b4bc5c2317fa4a56b3a8e3d8ba1d07619))
* **editor:** new wizard window design, update wizard links ([d4a58ef](https://gitlab.com/BennyKok/rhythm-core/commit/d4a58ef9f3892a0b1004078a95760bcb7e71573c))
* **editor:** new help comment style, toggleable help display in menu ([3469559](https://gitlab.com/BennyKok/rhythm-core/commit/3469559341b62e014c0155a90aab0b5e02e695ce))

## 0.7.0 (2020-09-19)


### ⚠ BREAKING CHANGES

* folder reorganized


## 0.6.0 (2020-09-18)

### Bug Fixes

- **midi:** make sure detected TimeSignature & TimeDivision unchanged ([7bd3a0a](https://gitlab.com/BennyKok/rhythm-core/commit/7bd3a0aacd50795c795d5373e8febea968dba1ac))
- 6 track prefab swapped track position ([12f8b66](https://gitlab.com/BennyKok/rhythm-core/commit/12f8b668eb90bde07b45ef770c450f21c627dd6b))

### Features

- added autoTimeScalePause in SongManager
- added UIEventTrackTrigger

### Refactor

- Removed singleton design, allowing multiple RhythmCore prefab

[Previous changelog](https://bennykok.gitbook.io/rhythm-game-starter/development/changelog)
