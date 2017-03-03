# Unity Pixel Camera

A resolution independent pixel perfect camera for Unity.

This package simplifies making a Unity camera render to exact pixel units, for use with pixel art or similar art styles where blockiness is part of the aesthetic.

## Features ##

* Simple setup
* Experimental perspective camera support

*Standard unity camera*

![Standard camera](http://i.imgur.com/pye9clh.gif)

*Pixel perfect camera*

![Pixel camera](http://i.imgur.com/VKhQrfu.gif)

## Basic Usage ##

1. Attach the `Pixel Camera` script to an existing camera.
2. Set `Pixels Per Unit` to an appropriate value, usually matching the settings used for your assets.
3. Set the `Zoom Level` for the camera.

## Advanced Settings ##

* __Camera Material__ - A material applied to the camera output, allows shaders to modify the image. The camera output is set as the `_MainTex` of the material.
* __Aspect Stretch__ - Apply a stretch to the output, allows the display to be non square pixels.
* __Down Sample__ - Scales down the render resolution, making the output blockier.
* __Perspective Z__ - Only for perspective cameras. The Z distance between the near and far clip planes, that is rendered as pixel perfect.

## Caveats ##

* If a camera or sprite is out of alignment with the pixel grid, unwanted artifacts may occur.
* Pixel Camera will not automatically zoom in or out according to the window/viewport size.
* Camera `Viewport Rect` settings are not taken into account.
* With a perspective camera, zoom levels below 1 will render a black border.
* Perspective camera rendering is unoptimized. High `Field of View` settings will generate unreasonably large RenderTextures. Use with caution.

## Technical Details ##

Pixel camera takes the size of the screen and finds the render size required to cover the screen in a pixel perfect manner, at the given settings.

A `RenderTexture` of the calculated render size is created, and if needed the camera settings are modified so the render fits the calculated size. The camera output is sent to the `RenderTexture`.

A dummy camera that renders nothing is created, and the `OnPostRender()` function is used to draw the output of the attached camera onto the screen using GL commands.

## API ##

### Properties ###

__ZoomLevel__ : float

The pixel zoom scale used by the camera.

__PixelsPerUnit__ : float

The pixels per unit value used by the camera.

__CameraMaterial__ : Material

The Material used to render the camera output, setting to `null` will use the default material. Pixel Camera sets the camera output as the `_MainTex` texture of the given material.

__AspectStretch__ : Vector2

An additional stretch applied to the camera, allows camera to render as non square pixels.

__DownSample__ : float

Scales down the render resolution, makes the output blockier. Minimum value is clamped at 1.

__PerspectiveZ__ : float

With a perspective camera, the distance between the camera near and far planes that is rendered as pixel perfect. Value is clamped between the near and far plane values.

__RenderTexture__ : RenderTexture, read only

Access the RenderTexture used as the camera output.

__CameraSize__ : int[], read only

Actual pixel size of the camera, as an integer array.

### Methods ###

__ForceRefresh()__ : void

Force the camera to recalculate rendering sizes.

__CheckCamera()__ : bool

Checks camera settings. If different from the last camera settings, will setup the camera again. Returns true if settings changed.
