using System;
using HC.Blazor.BlobStoring;
using HC.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp.Localization;

namespace HC.Blazor.Shared;

public class FileHelper
{
    protected IStringLocalizer<HCResource> L { get; }
    private readonly IBlobDisplayUrlProvider _blobDisplayUrlProvider;

    public FileHelper(IStringLocalizer<HCResource> localizer, IBlobDisplayUrlProvider blobDisplayUrlProvider)
    {
        L = localizer;
        _blobDisplayUrlProvider = blobDisplayUrlProvider;
    }

    public string GetImageUrl(string? imagePath)
    {
        return _blobDisplayUrlProvider.GetDisplayUrl(imagePath);
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
            "remove" => L["FilePicker:Remove"].Value,
            "Remove" => L["FilePicker:Remove"].Value,
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
            "Choose files: No file chosen" => L["FilePicker:NoFileChosen"],
            "ChooseFiles" => L["FilePicker:ChooseFiles"],
            "Or drop files here" => L["FilePicker:OrDropFilesHere"],
            "OrDropFilesHere" => L["FilePicker:OrDropFilesHere"],
            "ChooseFileOrDragAndDrop" => L["FilePicker:ChooseFileOrDragAndDrop"],
            "ChooseFileOrDragAndDropHere" => L["FilePicker:ChooseFileOrDragAndDropHere"],
            "ChooseFileOrDragAndDropHereToUpload" => L["ChooseFileOrDragAndDropHereToUpload"],
            "NoFileChosen"=> L["FilePicker:NoFileChosen"],
            "No file chosen"=> L["FilePicker:NoFileChosen"],
            "NoFileSelected"=> L["FilePicker:NoFileSelected"],
            "No file selected"=> L["FilePicker:NoFileSelected"],
            "Are you sure you want to remove the file?" => L["FilePicker:RemoveConfirmation"],
            "Are you sure you want to clear all files?" => L["FilePicker:ClearConfirmation"],
            "Are you sure you want to clear the selected files?" => L["FilePicker:ClearConfirmation"],
            "Ready to upload" => L["ReadyToUpload"],
            "Readytoupload" => L["ReadyToUpload"],
            "Uploaded successfully" => L["UploadedSuccessfully"],
            "Uploadedsuccessfully" => L["UploadedSuccessfully"],
            "Uploading" => L["Uploading"],
            _ => L[name].Value
        };
    }
}