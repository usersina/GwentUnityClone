using System.IO;
using UnityEngine;

public static class Tools
{
    public static void CheckMe()
    {
        Debug.Log("Essential tools are up and running.");
    }
    // ------------------------------------------------ Utility Functions---------------------------------------------//
    // Copy all of the folder (and its subdirectories and files into another folder)
    public static void CopyFolder(string sourcePath, string targetPath)
    {
        Copy(sourcePath, targetPath);
    }
    public static void Copy(string sourceDirectory, string targetDirectory)
    {
        var diSource = new DirectoryInfo(sourceDirectory);
        var diTarget = new DirectoryInfo(targetDirectory);

        CopyAll(diSource, diTarget);
    }
    public static void CopyAll(DirectoryInfo source, DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        // Copy each file into the new directory.
        foreach (FileInfo fi in source.GetFiles())
        {
            if (!fi.ToString().EndsWith(".meta"))
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
        }

        // Copy each subdirectory using recursion.
        foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir =
                target.CreateSubdirectory(diSourceSubDir.Name);
            CopyAll(diSourceSubDir, nextTargetSubDir);
        }
    }
}