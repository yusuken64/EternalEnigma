----------------------------------------
MAGIC ARSENAL by Archanor VFX
----------------------------------------

1. Introduction
2. Scaling effects
3. Asset Extras
4. URP Upgrade
5. Contact

----------------------------------------
1. INTRODUCTION
----------------------------------------

Effects can be found in the 'MagicArsenal/Effects/Prefabs'. Note that explosions prefabs are bundled with the missiles in 'Prefabs/Missiles & Explosions'.

To browse the effects in Unity, locate the scenes in 'MagicArsenal/Demo/Scenes' and add them to Build Settings before you start one of the scenes.

----------------------------------------
2. SCALING
----------------------------------------

To scale an effect in the scene, you can simply use the default Scaling tool (Hotkey 'R'). You can also select the effect and set the Scale in the Hierarchy.

Please remember that some parts of the effects such as Point Lights, Trail Renderers and Audio Sources may have to be manually adjusted afterwards.

----------------------------------------
3. ASSET EXTRAS
----------------------------------------

In the 'MagicArsenal/Effects/Scripts' folder you can find some neat scripts that may further help you customize the effects.

MagicBeamStatic - A simple script for beam effects. Use the prefabs from 'Effects/Beam/Setup' and experiment with the Beam Options!

MagicLightFade - This lets you fade out lights which are useful for explosions

MagicLightFlicker - A script for making pulsating and flickering lights

MagicRotation - A simple script that applies constant rotation to an object

----------------------------------------
4. URP Upgrade
----------------------------------------

If you want to upgrade the asset to work with URP, find the 'MagicArsenal/Upgrades' folder.

Inside you'll find two Unity Packages you need to install in order:

1. Magic Arsenal (URP Upgrade)
2. Magic Arsenal (URP 2022.3.21 Fix)

The second package will fix shader errors and some scene lighting issues.

If you launch the demo scenes, keep in mind that the shaders and materials may take a moment to load in after upgrading to URP.

Some particles may also be invisible after upgrading to URP as they have Soft Particles enabled. Make sure to enable Depth Texture in your project's rendering settings or disable Soft Particles in the particle materials.

----------------------------------------
5. CONTACT
----------------------------------------

Need help? Please visit my support webiste.

https://archanor.com/support.html

Thanks to mactinite for the sword model - http://opengameart.org/content/fantasy-sword-hand-painted-2
Thanke to HellGate for the cobblestone texture - https://opengameart.org/content/seamless-cobblestone-texture