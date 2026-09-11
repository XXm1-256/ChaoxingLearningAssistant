// Explicit global aliases for System.IO are intentional.
// Windows Desktop projects that combine WPF and WinForms should not rely on
// implicit using behavior for these core filesystem types.
global using File = System.IO.File;
global using Path = System.IO.Path;
global using Directory = System.IO.Directory;
