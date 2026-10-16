using System.Text;
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

//initiate Config Class. and display a usefull warning.
if ( SubTools.Config.Save_Skip_Backup ) SubTools.ConsoleHelper.Warning("auto backup disabled, if you have not made a backup of your files, please do so before proceeding.");

SubTools.UI.ParseInvoke( args );

/*
Console.ReadKey();
//ClearScreen("Squeaky clean =>");
*/
