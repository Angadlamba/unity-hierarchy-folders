# Folders for Unity Hierarchy

Simple specialized folder objects for Unity Hierarchy.

Designed to help with project organization, maintaining flexibility, not affecting performance at runtime, while not getting in the way in Editor.

## Features

1. Deletes themselves on **Play** and **Build** while keeping all children in place. Helping project organization in Editor while keeping performance at runtime.
2. Can be **Translated**, **Rotated** and **Scaled** freely just like any Game Object, yes the children will maintain the Transform modifications after the Folders delete themselves.
3. Behaves as a pivot that is always centered at their children for easy manipulation.
4. Works just fine with **RectTransforms**. (You can simply work normally as if the Folder is not even there).
5. Folder icon colors, for a little bit of customization.

#### Observations
> 1. For Folders to work correctly with **RectTransform**s it needs a **RectTransform** of it's own, to do this automatically just create a new Hierarchy Folder as a child of a Game Object that has a **RectTransform**, like a Canvas for example.
> 2. Changing the Folder's **RectTransform** values is not advised in order to maintain consistent results, I decided against hiding it just in case anyone needs to use it, but using Folders with **RectTransform**s was designed to be *set & forget*.
> 3. Keep in mind that when a child is reparented it's Transform values change based on the new parent's Transform values. If for some reason you have code that relies on a Game Object having very particular Transform values things will break if it is a child of a Folder, because, as expected, it will get reparented on Play/Build and it's Transform values will change. Also, If a folder is scaled all children will keep their local scale of 1, 1, 1 while parented to the Folder, but will inherit the Folder's scale once reparented. I consider this a feature, but it may trip up people that are not expecting it to be the case.

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

A "Create Folder" menu item should show up in the GameObject menu. Add
`Tests/Example.unity` to your current scene for an example of what hierarchy
folders can do for you.

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
