# Empyrion Configuration Editor
An application to simplify the handling and customizing of the `.ecf` configuration files of [Empyrion Galactic Survival](https://empyriongame.com/)

<img src="images/tool_teaser.png" title="Tool Teaser" width="1000" height="500"/>

## Content
- [Motivation](#motivation)
- [Installation](#installation)
- [Feature Overview](#feature-overview)
- [Operations Overview](#operations-overview)
- [Shortcuts and Functions](#shortcuts-and-functions)
- [File Content Definition](#file-content-definition)
- [File Content Recognition](#file-content-recognition)
- [Planned Major Features](#planned-major-features)

## Motivation
Over a long time lightweight modding [Empyrion Galactic Survival](https://empyriongame.com/) was really simple by adjusting the `config_example.ecf`. Due to the mechanic that the changes were added to the default settings, just the additional adjustments must be maintained. The decision of Eleon to remove these `add adjustments` feature simply felt like:

<img src="https://media.giphy.com/media/h36vh423PiV9K/giphy.gif" width="300" height="300">

Now all the whole bunch of tons of settings must be maintained at once even if adjusting just one tiny value of one silly block. The awkward `.ecf` format makes this feel like:

<img src="https://media.giphy.com/media/xT5LMAvRY92qUXj7dC/giphy.gif" width="300" height="250">

You know what i'm talking about? Here comes the solution!

<img src="https://media.giphy.com/media/5Y2bU7FqLOuzK/giphy.gif" width="300" height="250">

## Installation
Just download the latest release and unzip the content wherever you might need to. Run the executeable file and have fun!

## Feature Overview
### Content Definition
For each `.ecf` file the tool needs a definition. These definitions are located in `.xml` files in the `EcfFileDefinitions` sub folder of the zip package. For creating or adjusting the definitions yourself refer to [File Content Definition](#file-content-definition). After loading a `.ecf` file the definition is attached to it. To reinterprete a `.ecf` file with a different definition the `.ecf` file must be closed and reopened. The actual version is shipped with definitions for:
- `BlocksConfig.ecf`
- `Factions.ecf`
- `ItemsConfig.ecf`

### Content Recognition
At file loading the tool parses the file content according to the attached definition. The tool is balanced to the variety of the subtleties of the spellings in the `.ecf` files. Due to the design goal `the output should match input in at much details as possible` the tool in its release state will likely also report failures in the default `.ecf` files of the game. This is not a tool bug, but developer inaccuracies that may or may not be compensated for by a fallback. In order to achieve reliable behavior, I have chosen to report such bugs rather than legitimizing these inaccuracies by the definition. For details see [File Content Recognition](#file-content-recognition).

### Content Creation
At saving a `.ecf` file the whole content in the file is wiped and recreated.
```diff
- Any element with errors is NOT written to the file!
```
The error state is inherited structure upwards. A error of a subelement invalidates its container upto the root element. So pay attention to any error listed in the error report view and take care of it if you need the corresponding elements in the final `.ecf` file. 

### Language and Tool Support
Icons and Controls with complex behavior have tooltips on mouse over. All Labels and tootips are localised. At the moment de-DE and en-GB is supported.

### Tool Areas
#### File Operation Area
The standard file operations (new, open, reload, save, close) are located in this area. The cross-file functions (diff, merge, xml) are also arranged here.
<img src="images/file_operation_area.png" title="File Operation Area" width="1000" height="500"/>

#### Filter and File Selection Area
In this Area each opened file will get a tab containing the file name. The first label in the tool line indicates the attached content definition, for example `BlocksConfig`. The remaining icons provide different filter options applied to all content view areas.
<img src="images/filter_area.png" title="Filter Area" width="1000" height="500"/>

#### Content Operation Area
The tools in this area provide content altering options like adding, editing or removing elements. The copy/paste function is located here, too.  
<img src="images/content_operation_area.png" title="Content Operation Area" width="1000" height="500"/>

#### Tree View Area
The tree view area brings the structural overview. The root elements, child elements, parameters and comments are displayed in this view. If an element has an error the entry in this view will turn red.
<img src="images/tree_area.png" title="Tree View Area" width="1000" height="500"/>

#### Parameter View Area
The parameter view area shows the detail information of any parameter correlating to the selected tree element. Additionally the view analyzes and displays the inheritance dependancies to referenced elements to provide a overview over all parameters effecting the selected element. If an parameter has an error the entry in this view will turn red.
<img src="images/parameter_area.png" title="Parameter View Area" width="1000" height="500"/>

#### Info View Area
The info area displays additional detail information for the selected tree element and the selected parameter. Especially the element attributes (e.g `formatter`) can be found here.
<img src="images/info_area.png" title="Info View Area" width="1000" height="500"/>

#### Error View Area
In the error view all occured errors are listed. The errors belong to category `fatal`, `parsing` or `editing`. While `parsing` and `editing` are mostly correctable with the tool. The `fatal` ones violate the basic `.ecf` syntax and therefore cannot be imported.
<img src="images/error_area.png" title="Error View Area" width="1000" height="500"/>

## Operations Overview


## Shortcuts and Functions
- `double-click` opens the edit panel for the clicked item
- `right-click` opens the context panel for the clicked item
- `delete` removes the selected items
- `strg + c` copies the selected items to the clipboard
- `strg + v` pastes the copied items into the selected item, or after it if insertion is not allowed for the item

## File Content Definition
notes
- filename, foldername, template

## File Content Recognition
notes
- fatal errors, editing errors, parsing errors

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
