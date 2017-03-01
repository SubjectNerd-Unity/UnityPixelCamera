# Unity Pixel Camera

A resolution independent pixel perfect camera for Unity.

This package simplifies making a Unity camera render to exact pixel units, for use with pixel art or similar art styles where blockiness is part of the aesthetic.

*Standard unity camera*

![Standard camera](http://i.imgur.com/pye9clh.gif)

*Pixel perfect camera*

![Pixel camera](http://i.imgur.com/VKhQrfu.gif)

## Basic Usage ##

1. Attach the Pixel Camera script to an orthographic camera.
2. Set the Pixels Per Unit to an appropriate value, usually matching the settings used for your assets.
3. Set the Zoom Level for the camera.

## Advanced Usage ##

* Camera Material - allows a shader to be applied on the camera output. The script sets the camera output as the `_MainTex` of the material.
* Aspect Stretch - apply a custom stretch to the output, allowing your assets to be displayed in non square pixels.

## Caveats ##

* If a camera or sprite is out of alignment with the pixel grid, unwanted artifacts may occur
* This system will not automatically scale according to the window/viewport size

## API ##

### Properties ###

__ZoomLevel__ : float

The pixel scale used by the camera

__PixelsPerUnit__ : float

The pixels per unit value used by the camera

__CameraMaterial__ : Material

The Material used to render the camera output, setting to `null` will use the default material. Pixel camera will set the camera output as the `_MainTex` of the given material.

__AspectStretch__ : Vector2

An additional stretch applied to the camera, allows camera to render as non square pixels.

__RenderTexture__ : RenderTexture

Access to the RenderTexture used as the camera output.

__CameraSize__ : int[]

Actual pixel size of the camera, as an array.

### Methods ###

__ForceRefresh()__ : void
