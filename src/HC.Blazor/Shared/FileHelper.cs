using System;
using HC.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp.Localization;
namespace HC.Blazor.Shared;
public class FileHelper 
{
    protected IStringLocalizer<HCResource> L { get; }

    public FileHelper(IStringLocalizer<HCResource> localizer)
    {
        L = localizer;
    }
   
    public static string GetImageUrl(string apiBaseUrl,string imagePath)
    {
       if (string.IsNullOrEmpty(imagePath))
            return string.Empty;
            
        var baseUrl = apiBaseUrl ?? string.Empty;
        return $"{baseUrl}api/app/blob-files/file?path={Uri.EscapeDataString(imagePath)}";
    }

    public static bool IsPdfFileExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
            
        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".pdf";
    }


    public string FilePickerLocalizer(string name, params object[] arguments)
    {
        return name switch
        {
            "browse" => L["Browse"].Value,
            "clear" => L["Clear"].Value,
            "drop files here to upload" => L["DropFilesHereToUpload"].Value,
            "or" => L["Or"].Value,
            "upload" => L["Upload"].Value,
            "file" => L["File"].Value,
            "files" => L["Files"].Value,
            "remove" => L["Remove"].Value,
            "Remove" => L["Remove"].Value,
            "cancel" => L["Cancel"].Value,
            "close" => L["Close"].Value,
            "uploaded" => L["Uploaded"].Value,
            "uploading" => L["Uploading"].Value,
            "error" => L["Error"].Value,
            "file too large" => L["FileTooLarge"].Value,
            "invalid file type" => L["InvalidFileType"].Value,
            "ClearConfirmation" => L["FilePicker:ClearConfirmation"],
            "Clear" => L["Clear"],
            "Cancel" => L["Cancel"],
            "Confirm" => L["Confirm"],
            "ChooseFile" => L["FilePicker:ChooseFile"],
            "Choose files" => L["FilePicker:ChooseFiles"],
            "ChooseFiles" => L["FilePicker:ChooseFiles"],
            "Or drop files here" => L["FilePicker:OrDropFilesHere"],
            "OrDropFilesHere" => L["FilePicker:OrDropFilesHere"],
            "ChooseFileOrDragAndDrop" => L["FilePicker:ChooseFileOrDragAndDrop"],
            "ChooseFileOrDragAndDropHere" => L["FilePicker:ChooseFileOrDragAndDropHere"],
            "ChooseFileOrDragAndDropHereToUpload" => L["ChooseFileOrDragAndDropHereToUpload"],
            "No file chosen"=> L["FilePicker:NoFileChosen"],
            "NoFileSelected"=> L["FilePicker:NoFileSelected"],
            "Are you sure you want to remove the file?" => L["FilePicker:RemoveConfirmation"],
            "Are you sure you want to clear all files?" => L["FilePicker:ClearConfirmation"],
            "Are you sure you want to clear the selected files?" => L["FilePicker:ClearConfirmation"],
            _ => L[name].Value
        };
    }
}