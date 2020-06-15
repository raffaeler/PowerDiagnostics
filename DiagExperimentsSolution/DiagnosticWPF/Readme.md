# Demo UI

The general idea behind the whole demo is to automate the diagnostic process.
This concept goes into the opposite direction of generic UIs like this.
Also, I specifically **avoided** the MVVM pattern to make the demo more comprehensible to the people that is not familiar with WPF and the MVVM pattern.
If this application was a production application, the MVVM would be the better choice.

The rationales behind this UI are:
- Provide a better way to show the outcome of the ClrDiagnostics library
- Simplify as much as possible the scenario by loading either a dump or snapshotting a live process
- Quickly jump from one analysis to another one, without having to patch the code continuously.

Be aware that the UI is tied to Windows while the diagnostics library are cross-platform.

If you have questions, just ping me at @raffaeler.

Hope you will enjoy this :)

Last revision: June 2020

## UI Structure

In order to show many different types of data inside the same grid, there are two main types:
- UIGrid describes how a type should be displayed in the grid. When specified, DetailsProperty represents the property with a collection of other objects to be shown in the details grid.
- UIGridColumn describes the data to configure a DataGridTemplateColumn
- Other UI types. The ClrDiagnostics library heavily uses ValueTuple type embedded in the C# language. Unfortunately the reflection code used by WPF is not able to reflect the fields, therefore the data has to be copied into objects defined by classes.
- KnownGrids is a static type that defines (using UIGrid and UIGridColumn) how each type is shown inside the grids.

