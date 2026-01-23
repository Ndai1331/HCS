using System.Collections.Generic;

namespace HC.Chat.Helpers;

public static class FileHelper
{
    public static List<string> GetFileExtensions(Conversations.FileMediaType fileType)
    {
        List<string> fileExtensions = new();
        if (fileType == Conversations.FileMediaType.Image)
        {
            fileExtensions.Add("png");
            fileExtensions.Add("jpg");
            fileExtensions.Add("jpeg");
            fileExtensions.Add("gif");
            fileExtensions.Add("bmp");
            fileExtensions.Add("webp");
            fileExtensions.Add("svg");
            fileExtensions.Add("ico");
            fileExtensions.Add("heic");
            fileExtensions.Add("heif");
            fileExtensions.Add("hevc");
            fileExtensions.Add("heifc");
            fileExtensions.Add("hevc");
            fileExtensions.Add("heifc");
            fileExtensions.Add("hevc");
            fileExtensions.Add("heifc");
        }
        else
        {
            fileExtensions.Add("pdf");
            fileExtensions.Add("doc");
            fileExtensions.Add("docx");
            fileExtensions.Add("xls");
            fileExtensions.Add("xlsx");
            fileExtensions.Add("ppt");
            fileExtensions.Add("pptx");
            fileExtensions.Add("txt");
            fileExtensions.Add("csv");
            fileExtensions.Add("tsv");
            fileExtensions.Add("json");
            fileExtensions.Add("xml");
            fileExtensions.Add("html");
            fileExtensions.Add("css");
            fileExtensions.Add("js");
            fileExtensions.Add("php");
            fileExtensions.Add("java");
            fileExtensions.Add("cpp");
            fileExtensions.Add("c");
            fileExtensions.Add("h");
            fileExtensions.Add("hpp");
            fileExtensions.Add("hxx");
            fileExtensions.Add("cxx");
            fileExtensions.Add("c++");
            fileExtensions.Add("c#");
            fileExtensions.Add("vb");
            fileExtensions.Add("vbnet");
        }
        return fileExtensions;
    }
}