#Dynamo: Visual Programming for Revit#

## Description ##
The intent of this project is to provide a visual interface for building interesting parametric functionality on top of that already offered by Revit. Dynamo aims to be accessable both to the non-programmer and the programmer alike with the ability to visually script behavior and define your own nodes, but also the ability to write functionality using Python or by compiling .net code into dlls that can be linked at run time.

## Contributors ##

This project was started by Ian Keough. A complete rewrite of the underlying Dynamo engine was done by Stephen Elliot and Matt Jezyk contributed a ton of nodes as well as a wealth of input on pretty much all other aspects of Dynamo's design. Prior to Ian's joining Autodesk, others on the Autodesk team including Zach Kron, Tom Vollaro, and Lillian Smith provided a lot of very useful feedback.


Dynamo has been developed based on feedback from several parties inlcuding Buro Happold Engineers, Autodesk, and students and faculty at the USC School of Architecture.


## Running Dynamo ##

The current version will run on top of Revit 2013 and Project Vasari Beta 2. It will be released as a new Project Vasari WIP soon but is available now experimental form on github.

## Releases ##

###November 2012:###

New or updated nodes:

- Added slider value display when slider is moved
- Added XYZ utility nodes (XYZ Zero, XYZ Basis, XYZ Scale, XYZ Add, Evaluate Normal, Evaluate XYZ)
- Added XYZ Number Extractor nodes (XYZ -> X, XYZ -> Y, XYZ -> Z)
- Added Reference Point By Normal node
- Fixed Arduino node, it now works with new framework
- Added Execution Interval (Timer) node
- Added Spatial Field Manager node and Analysis Results node
- Added Level node and Level by Selection node
- Added Planar Nurbs Spline node
- Added Divided Surface node
- Added Divided Path node
- Updated Lofted Form node to handle surfaces vs solids and solids vs voids
- Added Chomp, Dice, LaceForward and LoacBackwards definitions for working with 2D arrays.
- Added new string utility nodes 
- Added a CSV file writer and basic file writer nodes
- Added a Watch node
- Added Dynamic Relaxation node (still experimental)

UI:
- Added abilty to create Notes in graphics window
- The built-in samples in File/Samples have been reorganized to use subfolders
- added File/Save command
- Pressing the Dynamo Ribbon button in Revit/Vasari while Dynamo is already running now simply makes Dynamo window visible again
- It is now possible to move around the graphics screen with the arrow keys.
- If second monitor is available, Dynamo will maximize to it.

### June 2012:###

- User Interface - side panel to allow you to search and then drag and drop new nodes in to place
- User-created nodes - you can make 'sub-nodes' and then reference them elsewhere, these act like writing a reusable function in a programming language
- Python Scripting node - we have integrated a python scripting node into 
- Math, logic and other nodes - many new nodes added to support evaluation,  iteration and looping

Dynamo For Vasari Beta 2 WIP: Integrating Dynamo into Project Vasari and extending Dynamo to support integrated analysis and performance-based design.


## License ##

Those portions created by Ian are provided with the following copyright:

Copyright 2013 Ian Keough

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
