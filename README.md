# Folders for Unity Hierarchy

Simple specialized folder objects for Unity Hierarchy.

Designed to help with project organization, maintaining flexibility, not affecting performance at runtime, while not getting in the way in Editor.

## Features

1. `Folder`s Deletes themselves on **Play** and **Build** while keeping all children in place. Helping project organization in Editor while keeping performance at runtime.
2. Create a `Folder` by right-clicking in the Hierarchy and selecting "**Create Hierarchy Folder**".
3. Select the deep children of a `Folder` by right-clicking it and selecting **Folder/Select Deep Children**.
4. `Folder`s are locked at **Pos: 0,0,0**; **Rot: 0,0,0**; **Scale: 1,1,1** making it so that all children `Transform` values will be equal to their worldspace values, making it more intuitive to work with them.
5. Hides transformation tools while a `Folder` is selected to prevent accidental transformations.
6. Works just fine with `RectTransforms`. (*You can simply work normally as if the Folder is not even there*).
7. Folder icon colors, for a little bit of customization.

#### Observations
> For Folders to work correctly with `RectTransform`s it needs a `RectTransform` of it's own. To do this automatically just create a new Hierarchy Folder as a child of a `GameObject` that has a `RectTransform`, like a Canvas for example.

## Installation

This uses the new UPM system. The old copy-into-Assets method still works
perfectly decent so if you don't want to bother with UPM just copy the `Editor`
and `Runtime` folders into your project.

To add this project, add a [git dependency][1] in your `manifest.json`:

```json
{
  "dependencies": {
    "com.unity.package-manager-ui": "1.9.11",
    "com.xsduan.hierarchy-folders": "https://github.com/xsduan/unity-hierarchy-folders.git"
  }
}
```

Older versions of Unity may have to use the relative link, ie:

```json
{
  "dependencies": {
    "com.unity.package-manager-ui": "1.9.11",
    "com.xsduan.hierarchy-folders": "file:../../unity-hierarchy-folders"
  }
}
```
The UPM does not have much documentation at the moment so it probably will be
buggy, you're not going crazy!

[1]: https://forum.unity.com/threads/git-support-on-package-manager.573673/#post-3819487

### OpenUPM

Please note that this is a third party service, which means that Unity
Technologies will not provide support. Always be mindful when considering
unofficial plugins and tools.

```
$ openupm add com.xsduan.hierarchy-folders
```

To install OpenUPM, please see the [documentation][2].

[2]: https://openupm.com/docs/

## Possible FAQs

### Why folders in the first place?

As projects get bigger, they tend to get cluttered in the scene. It's very
helpful if you can group them together into logical groups.

#### Why delete them on build then?

Because they are best used for level designers to declutter the hierarchy, but
calculating the global transform from the local during runtime can take a
noticeable impact on performance once scenes get to 1000, 10000, or more
objects.

#### So why can't I just use empty GameObjects and delete them on build?

Sometimes empty GameObjects are used for other things and it's useful to have a
specific type of object that should always be deleted on build.

Besides, I did all the legwork, so you wouldn't have to!

### There's another product/widget that exists that does this exact task.

So there are. This isn't exactly a unique concept and I only made it for future
personal use and shared it only to possibly to help other people because I
couldn't find it on Google.

If you are the owner of one such product, please contact me and we can work
something out.

The hope is to have it be a native component like it is in Unreal. (Not
necessarily this one specifically, but I'm not opposed to it ;) I've seen paid
components for this and frankly for the effort it took me it's a bit of a
rip-off to pay any amount for it.
