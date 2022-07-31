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
For each `.ecf` file the tool needs a definition. These definitions are located in `.xml` files in the `EcfFileDefinitions` sub folder of the zip package. For creating or adjusting the definitions yourself refer to [File Content Definition](#file-content-definition). After loading a `.ecf` file the definition is attached to it. To reinterprete a `.ecf` file with a different or changed definition the `.ecf` file must be closed and reopened. The actual version of the tool is shipped with definitions for:
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
The error state is inherited structure upwards. A error of a sub element invalidates its containing element upto the root element. So pay attention to any error listed in the error report view and take care of it if you need the corresponding elements in the final `.ecf` file. Due to the nature of the `.ecf` syntax of consecutive specifications its recommended to
```diff
- solve errors from top to bottom
```
to not follow rabbits.

### Language and Tool Support
Icons and Controls with complex behavior have tooltips on mouse over. All Labels and tootips are localised. At the moment de-DE and en-GB is supported. The language switches automaticly based on the local machine culture setting, defaulting to en-GB.

### Tool Overview
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
In the error view all occured errors are listed. The errors belong to category `fatal`, `parsing` or `editing`. While `parsing` and `editing` are mostly correctable with the tool. The `fatal` ones violate the basic `.ecf` syntax and therefore cannot be imported. Due to the non-saving of elements with errors the list is cleared at file saving.

<img src="images/error_area.png" title="Error View Area" width="1000" height="500"/>

### Mass Changing
:wrench: Not implemented yet :wrench:

### Compare and Merge
:wrench: Not implemented yet :wrench:

### Tech Tree Preview
:wrench: Not implemented yet :wrench:

## Operations Overview
### Opening a file
After selecting the file to open the tool tries to guess which definition fits to the file by examining the file name. If this is successful the guessed definition is automaticly selected in the following dialog. In this case the Dialog can be skipped with `Ok`. When the guessing fails or a new file is created the definition must be selected manually from the provided drop down list.

<img src="images/preload_settings.png" title="File Property Selector"/>

For especially big files like `BlocksConfig.ecf` (or especially lame PCs :zany_face:) a loading dialog is shown while parsing the file content. 

<img src="images/loading_content.png" title="Loading Dialog"/>

### Adding And Editing Content
At adding or editing (see [Shortcuts and Functions](#shortcuts-and-functions)) the editor dialog is shown. The dialog is designed to not produce elements with errors. To achive this support pre opening checks, pre filled selection lists and pre closing checks will be performed. These logics depends primary on the attached file definition and the present content data.

The only exception is the creation of a completely new root element. At the creation of a root element all mandatory (see [File Content Definition](#file-content-definition)) parameters will be added without values. This will create errors until the mandatory parameters will be filled with values.

<img src="images/editing_dialog.png" title="Editing Dialog"/>

## Shortcuts and Functions
### Shortcuts
- `double-click` opens the edit panel for the clicked item
- `right-click` opens the context panel for the clicked item
- `delete` key removes the selected items
- `strg + c` key copies the selected items to the clipboard
- `strg + v` key pastes the copied items into the selected item, or after it if insertion is not allowed for the item

### Context Panel
#### Tree View Context Panel
The context panel of the tree view provides access to the content operations suitable for tree items.

<img src="images/tree_context.png" title="Tree Context Panel"/>

#### Parameter View Context Panel
The context panel of the parameter view provides access to the content operations suitable for parameter items.

<img src="images/parameter_context.png" title="Parameter Context Panel"/>

#### Error View Context Panel
The context panel of the error view has support functions for error tracking.

<img src="images/error_context.png" title="Error Context Panel"/>

### Sorting
The three listing views [Tree View Area](#tree-view-area), [Parameter View Area](#parameter-view-area) and [Error View Area](#error-view-area) have a sorting panel each. The sort panel provides settings for concurrent items and various sorting options.

<img src="images/sorting_panel.png" title="Sorting Panel"/>

### Icons
#### File Operations
- <img src="EgsEcfControls\Resources\Icon_NewFile.png" title="icon" width="16" height="16"/> create new file
- <img src="EgsEcfControls\Resources\Icon_OpenFile.png" title="icon" width="16" height="16"/> open existing file
- <img src="EgsEcfControls\Resources\Icon_ReloadFile.png" title="icon" width="16" height="16"/> reload content from selected file
- <img src="EgsEcfControls\Resources\Icon_SaveFile.png" title="icon" width="16" height="16"/> save selected file
- <img src="EgsEcfControls\Resources\Icon_SaveAsFile.png" title="icon" width="16" height="16"/> save selected file with new name
- <img src="EgsEcfControls\Resources\Icon_SaveAsFilteredFile.png" title="icon" width="16" height="16"/> save filter result to file
- <img src="EgsEcfControls\Resources\Icon_SaveAllFiles.png" title="icon" width="16" height="16"/> save all opened files
- <img src="EgsEcfControls\Resources\Icon_CloseFile.png" title="icon" width="16" height="16"/> close selected file
- <img src="EgsEcfControls\Resources\Icon_CloseAllFiles.png" title="icon" width="16" height="16"/> close all open files
- <img src="EgsEcfControls\Resources\Icon_Compare.png" title="icon" width="16" height="16"/> compares two files and displays differences (see [Compare and Merge](#compare-and-merge))
- <img src="EgsEcfControls\Resources\Icon_Merge.png" title="icon" width="16" height="16"/> merge two files to one (see [Compare and Merge](#compare-and-merge))
- <img src="EgsEcfControls\Resources\Icon_BuildTechTreePreview.png" title="icon" width="16" height="16"/> displays the tech tree preview (see [Tech Tree Preview](#tech-tree-preview))
- <img src="EgsEcfControls\Resources\Icon_ReloadDefinitions.png" title="icon" width="16" height="16"/> reloads the `.xml` definitions (see [File Content Definition](#file-content-definition))
- <img src="EgsEcfControls\Resources\Icon_CheckDefinition.png" title="icon" width="16" height="16"/> checks the `.xml` definitions (see [File Content Definition](#file-content-definition))

#### Filter and Sorting Operations
- <img src="EgsEcfControls\Resources\Icon_ApplyFilter.png" title="icon" width="16" height="16"/> apply filter
- <img src="EgsEcfControls\Resources\Icon_ClearFilter.png" title="icon" width="16" height="16"/> clear filter
- <img src="EgsEcfControls\Resources\Icon_ShowAllItems.png" title="icon" width="16" height="16"/> show item with / without error
- <img src="EgsEcfControls\Resources\Icon_ChangeSortDirection.png" title="icon" width="16" height="16"/> switch the sort direction
- <img src="EgsEcfControls\Resources\Icon_NumericSorting.png" title="icon" width="16" height="16"/> sets sorting to the origin (file) order
- <img src="EgsEcfControls\Resources\Icon_AlphabeticSorting.png" title="icon" width="16" height="16"/> sets sorting to alphabetic order

#### Content Operations
- <img src="EgsEcfControls\Resources\Icon_Add.png" title="icon" width="16" height="16"/> add item to / after selection
- <img src="EgsEcfControls\Resources\Icon_ChangeSimple.png" title="icon" width="16" height="16"/> editing first selected item
- <img src="EgsEcfControls\Resources\Icon_ChangeComplex.png" title="icon" width="16" height="16"/> editing items with logics (see [Mass Changing](#mass-changing))
- <img src="EgsEcfControls\Resources\Icon_Remove.png" title="icon" width="16" height="16"/> removing selected items
- <img src="EgsEcfControls\Resources\Icon_Copy.png" title="icon" width="16" height="16"/> copy selected items
- <img src="EgsEcfControls\Resources\Icon_Paste.png" title="icon" width="16" height="16"/> paste copied items to / after selection
- <img src="EgsEcfControls\Resources\Icon_Undo.png" title="icon" width="16" height="16"/> undo last change
- <img src="EgsEcfControls\Resources\Icon_Redo.png" title="icon" width="16" height="16"/> redo last undo
- <img src="EgsEcfControls\Resources\Icon_MoveUp.png" title="icon" width="16" height="16"/> move selected items up
- <img src="EgsEcfControls\Resources\Icon_MoveDown.png" title="icon" width="16" height="16"/> move selected items down
- <img src="EgsEcfControls\Resources\Icon_AddValue.png" title="icon" width="16" height="16"/> add value slot after selection
- <img src="EgsEcfControls\Resources\Icon_AddValueGroup.png" title="icon" width="16" height="16"/> add value group after selection
- <img src="EgsEcfControls\Resources\Icon_RemoveValue.png" title="icon" width="16" height="16"/> remove selected value slot
- <img src="EgsEcfControls\Resources\Icon_RemoveValueGroup.png" title="icon" width="16" height="16"/> remove selected value group

#### Error Operations
- <img src="EgsEcfControls\Resources\Icon_ShowInEditor.png" title="icon" width="16" height="16"/> show error in tool
- <img src="EgsEcfControls\Resources\Icon_ShowInFile.png" title="icon" width="16" height="16"/> show error in file

## File Content Definition
The file content definition is the basic information for the tool which content is viable in the loaded `.ecf` file and which is not. To achive the design goal to be reliable able to load and interprete `.ecf` files from any source (default files, text editor tool files, manually edited files and so on) the definition provides several options. 

The defintions will be read from the `EcfFileDefinitions` sub folder from the tool root directory. The file name doesn't matter. Each file in this directory will be read. The first entry in the file is the definition name shown in the tool, the link to the corresponding `.ecf` file (for guessing) and must be unique. Every further `.xml` file with the same `type` setting will be ignored.

<img src="images/xml_type.png" title="XML Type"/>

If the `EcfFileDefinitions` sub folder is missing or empty the tool will create a simple template `.xml` file for `BlocksConfig.ecf`. This file can be used as starting point for creation of own files and is structural complete, but will likely be incomplete compared to the expected `.ecf` content.

After starting the tool the present `.xml` files are loaded and prepared for use. Changing the `.xml` files after running the tool will not be recognized by the tool. If you want the tool to reload the definitions just hit the respective button ( <img src="EgsEcfControls\Resources\Icon_ReloadDefinitions.png" title="icon" width="16" height="16"/> ). Due to the intended behaviour to not alter the definitions of opened `.ecf` files, reloading the '.xml' definitions not changing already opened `.ecf` files. 

After bigger patches of [Empyrion Galactic Survival](https://empyriongame.com/) a definition maybe can partly went deprecated. In this case a backwards check of the definition is recommended. This can be done (if I'm too lame to release an update :sleeping:) by hitting the respective button ( <img src="EgsEcfControls\Resources\Icon_CheckDefinition.png" title="icon" width="16" height="16"/> ). This function compares the actual selected `.ecf` file against the latest loaded `.xml` definition of the same type and lists the `.xml` content definition for which no content is found in the `.ecf` file.

### Xml File Content
The definition uses often the terminology `Block`. Don't missinterprete this with an `Empyrion ingame block`. In Xml-speaking `Block` stands for a `.ecf data block`.

The `formatting` chapter of the `.xml` files provides settings for the basic `.ecf` syntax. This chapter should mostly be similar for all `.ecf` files.

<img src="images/xml_formatting.png" title="XML ECF Formatting"/>

After the `formatting` follow the sub chapters for the different elements of the `.ecf` files. This chapter contains the expected `.ecf` content.

<img src="images/xml_ecf_content.png" title="XML ECF Content"/>

The available chapters are:

- `BlockTypePreMarks` the expected pre marks after the block opener (e.g. `+`)
- `BlockTypePostMarks` the expected seperators to define the end of the block type (e.g. ` `)
- `RootBlockTypes` the expected data types of blocks in the root level (e.g. `Block`)
- `RootBlockAttributes` the expected attributes for blocks in the root level (e.g. `Id`) 
- `ChildBlockTypes` the expected data types of blocks not in the root level (e.g. `Child`)
- `ChildBlockAttributes` the expected attributes for blocks not in the root level (e.g. `DropOnDestroy`)
- `BlockParameters` the expected parameters for the data blocks (e.g. `Material`)
- `ParameterAttributes` the expected attributes for the parameters (e.g. `formatter`)

Each `.xml Param` line item can hold different switches to control the behaviour of the error checking while parsing and editing the `.ecf` content. The chapters are allowed to be empty, but remind that the respective functions in the [Editor Panel](#adding-and-editing-content) will be switched off without the definition.

<img src="images/xml_options.png" title="ECF Options"/>

- `name` the name of the `.ecf` element (e.g. `Material`)
- `optional` determines if this element is optional, non-optional elements will be treated as mandatory and can in some cases overwrite optional ones
- `hasValue` determines if this element has (reading) / should have (creation) a value
- `allowBlank` determines if for this element a `empty value` is allowed 
- `forceEscape` determines if at creation this element must be escaped, useful for text references like `TechTreeParent`
- `info` a free text to preserv designer infos for the element, shown in the tool, not written to the file

## File Content Recognition
The Parsing of the `.ecf` tries to interprete the whole variety of spellings present in the different files. To minimize result validation efforts and to preserv the readability of the files by [Empyrion Galactic Survival](https://empyriongame.com/), the tool reproduces nearly the same spelling as the original file.

The exceptions are the white spaces, the empty lines and the comments. All the function-irrelevant white spaces and empty lines will be remove. All the different varieties of comment spellings will be streamlined to appended inline comments.

The tool parses the `.ecf` file content line by line and seperates the line content item by item according to the definition and chronologic. A fault can depending on its severity lead to a whole bunch of follower errors. This is the reason of solving errors from top to bottom.

For the content recognition and operation three groups of errors are possible.

### Fatal Error
This errors occur during the parsing of the content at the loading of the `.ecf` file. The corresponding line in the file violates the syntax in a manner which makes it impossible to attach this data to the managed structure within the tool. Such an error must be corrected in the original file if the data is needed.

### Parsing Error
This errors occur during the parsing of the content at the loading of the `.ecf` file. It can in mostly all cases be corrected in the tool.

### Editing error
This errors occur at content editing operations in the tool which would consume too much system recources to prevent it by a pre check. It can in all cases be corrected in the tool.

## Planned Major Features
:wrench::wrench::wrench: The next steps will be the compare / merge feature together with more supported files (included Reforged Eden). :wrench::wrench::wrench:

- Support for Reforged Eden
- Support for all .ecf files
- Compare files view
- Merge files with behaviour selection
- Element, Parameter, Comment moving
- TechTree Preview
- Element, Parameter, Attribute, Comment mass changing (base on filter/types)
- SaveAs with taking applied filter into account
- Undo / Redo
