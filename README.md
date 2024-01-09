# FACS UnityPackage Maker

Tool to create/extract Unity Packages without Unity

# How to use

```sh
FUPM.exe [work] [source] [destination] {optional "noConsole"}
```

`[work]`:
  - `P`: create a new Unity Package. The selected Source_Folder should be a Unity project's "Assets" or "Packages" folder (a "root" folder).
  - `PA`: create a new Unity Package with root folder "Assets". When imported into Unity, will be unpacked into "Assets/Source_Folder/".
  - `PP`: create a new Unity Package with root folder "Packages". When imported into Unity, will be unpacked into "Packages/Source_Folder/".
  - `U`: unpack a Unity Package, removing the root folder. For example, instead of unpacking into "Assets/assetFoder1/assetFolder2/...", it would unpack into "assetFoder1/assetFolder2/...".
  - `UK`: unpack a Unity Package, keeping the root folder.

`[source]`: path to the Source_Folder to pack, or to the Unity Package file to unpack.

`[destination]`: path to the new Unity Package to create, or to a new|empty folder to unpack into.

When packing a new Unity Package, files and folders without a .meta file will get a ramdomized GUID. So yes, you can pack assets with and without .meta files.

# Motivation to make this tool

I wanted to export an embedded Package as a Unity Package that would be able to be imported into other project's Packages folder, but Unity doesn't allow to create Unity Packages with assets from the Packages folder (at least not from the Right-Click context menu).

# Caveats

This tool was meant to be used to pack only 1 folder (with all assets and folders required inside) under the Assets or Packages folder.
I didn't want to support dumping multiple folders inside Packages for example, as each could be its own embedded package in its own Unity Package.
At the end I added that functionality anyways (in [work]="P"), but you would have to stick to the same root folder instead of being able to change it on the fly (as in [work]={"PA"|"PP"}).

# Examples

* To make a Unity Package out of the folder "testFolder", that would be extracted into the project's Packages folder, and save it as "testPackage.unitypackage":

```sh
FUPM.exe PP testFolder testPackage.unitypackage
```

* To unpack "testPackage.unitypackage", omitting "Packages" from the extracted paths, and dumping its content into "testPackageDump" (result would be "testPackageDump/testFolder/"):

```sh
FUPM.exe U testPackage.unitypackage testPackageDump
```

* To unpack and repack any kind of Unity Package (having 1 folder or having multiple folders and assets under root):

```sh
FUPM.exe UK anyPackage.unitypackage anyPackageDump
```

```sh
[FUPM.exe P anyPackageDump/Assets anyPackageNew.unitypackage]
```
or
```sh
[FUPM.exe P anyPackageDump/Packages anyPackageNew.unitypackage]
```
