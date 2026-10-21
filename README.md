# Founders: First Civilization

A mobile-first civilization and survival city builder with a large, explorable island. The visual direction takes cues from the readable, painterly presentation of PC city builders such as Foundation, while the world, story, and progression are original.

## First milestone: island terrain

The first Unity milestone is a single island scene with open ocean, a mineral-rich mountain, a prominent waterfall, a river and lake, a volcano at the far end, and forests, rocks, and wildflowers. Natural props are placed as separate scene objects so they can later be selected, removed, and replaced through gameplay.

The next milestones will add a touch-friendly camera and terrain editing, free placement on land, docks and other era-appropriate structures over water, building systems, survival needs, resources, and progression through eras.

## Open in Unity

Open the repository root as a Unity project using Unity 2022.3 LTS. After the first import, use **Founders > Generate Island Prototype** to create `Assets/Scenes/Island.unity`.

## Build for iOS

Select the iOS build target in Unity and build the Xcode project. An installable `.ipa` requires signing with an Apple development certificate and provisioning profile on macOS. Install the signed IPA with Feather. Never commit signing certificates, private keys, provisioning profiles, or account secrets.

`Library/`, temporary files, and build output are excluded from version control. Keep the project source and generated scene assets in GitHub so work can continue across devices.