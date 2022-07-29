# EgsEcfEditorApp
An application to simplify the handling and customizing of the .ecf configuration files of [Empyrion Galactic Survival](https://empyriongame.com/)

<img src="images/tool_teaser.png" title="Tool Teaser" width="1000" height="500"/>

## Content
- [Motivation](#motivation)
- [Installation](#installation)
- [Feature Overview](#feature-overview)
- [Operations Overview](#operations-overview)
- [Shortcuts and Icons](#shortcuts-and-icons)
- [File Content Definition](#file-content-definition)
- [File Content Recognition](#file-content-recognition)
- [Planned Major Features](#planned-major-features)

## Motivation
Over a long time lightweight modding [Empyrion Galactic Survival](https://empyriongame.com/) was really simple by adjusting the `config_example.ecf`. Due to the mechanic that the changes were added to the default settings, just the additional adjustments must be maintained. The decision of Eleon to remove these `add adjustments` feature simply felt like:

<img src="https://media.giphy.com/media/h36vh423PiV9K/giphy.gif" width="300" height="300">

Now all the whole bunch of tons of settings must be maintained at once even if adjusting just one tiny value of one silly block. The awkward `.ecf` format makes this feel like:

<img src="https://media.giphy.com/media/xT5LMAvRY92qUXj7dC/giphy.gif" width="300" height="250">

To all who know what i'm talking about here comes the solution!

<img src="https://media.giphy.com/media/5Y2bU7FqLOuzK/giphy.gif" width="300" height="250">

## Installation
Just download the latest release and unzip the content wherever you might need to. Run the portable executeable file and have fun!

## Feature Overview
### Definition
For each `.ecf` file the tool needs a definition. These definitions are located in `.xml` files in the `` sub folder of the zip package. For creating or adjusting the definitions refer to [File Content Definition](#file-content-definition). After loading a `.ecf` file the definition is attached to it. To reinterprete a `.ecf` file with a different definition the `.ecf` file must be closed and reopened.

## Operations Overview


## Shortcuts and Icons
- `double-click` opens the edit panel for the clicked item
- `right-click` opens the context panel for the clicked item
- `delete` removes the selected items
- `strg + c` copies the selected items to the clipboard
- `strg + v` pastes the copied items into the selected item, or after it if insertion is not allowed for the item

## File Content Definition


## File Content Recognition


## Planned Major Features
- Support for all .ecf files
- Compare files view
- Merge files with behaviour selection
- Element, Parameter, Comment moving
- TechTree Preview
- Element, Parameter, Attribute, Comment mass changing (base on filter/types)
- SaveAs with taking applied filter into account
- Undo / Redo

The next steps will be the compare / merge feature together with more supported files.
