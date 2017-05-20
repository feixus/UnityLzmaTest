using UnityEngine;
using System.Collections;
using System;
using System.IO;
using System.Text;
using UnityEditor;
using SevenZip.Compression.LZMA;
using System.Collections.Generic;

public class ABTest : EditorWindow
{
    private static ABTest window;

    static List<UnityEngine.Object> resourcesLis = new List<UnityEngine.Object>();
    static Dictionary<string, UnityEngine.Object> resourcesDic = new Dictionary<string, UnityEngine.Object>();

    UnityEngine.Object lisObj ;
    Vector2 scrollPos = new Vector2(0, 0);
    

    [MenuItem("UnityEditor/LZMA")]
    private static void LZMATest()
    {
        window = EditorWindow.GetWindow<ABTest>();

        window.titleContent = new GUIContent("LZMA");
    }

    void OnGUI()
    {
        EditorGUILayout.ObjectField(lisObj, typeof(UnityEngine.Object), true);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < resourcesLis.Count; i++)
        {
            EditorGUILayout.ObjectField(resourcesLis[i], typeof(UnityEngine.Object), true);
        }

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            string[] filePath = DragAndDrop.paths;
            if (filePath == null || filePath.Length <= 0)
            {
                return;
            }

            for (int i = 0; i < filePath.Length; i++)
            {
                GetAssetsToLis(filePath[i]);
            }

            AssetDatabase.Refresh();
        }

        if (GUILayout.Button("Clear"))
        {
            resourcesLis.Clear();
            resourcesDic.Clear();
        }

        if (GUILayout.Button("Compress"))
        {
            CompressLzma();
        }

        if (GUILayout.Button("DeCompress"))
        {
            DecompressLzma();
        }
    }

	/// <summary>
	/// collect assets to resourcesDic
	/// </summary>
	/// <param name="assetPath">Asset path.</param>
    private static void GetAssetsToLis(string assetPath)
    {
        if(Directory.Exists(assetPath))    // i only set directory drop or drap
        {
            string[] filePaths = Directory.GetFiles(assetPath, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < filePaths.Length; i++)
            {
                if(filePaths[i].Contains(".meta"))
                {
                    continue;
                }

                if(!resourcesDic.ContainsKey(filePaths[i]))
                {
                    resourcesDic.Add(filePaths[i], AssetDatabase.LoadMainAssetAtPath(filePaths[i]));
                    resourcesLis.Add(AssetDatabase.LoadMainAssetAtPath(filePaths[i]));
                }
            }
        }
    }


	/// <summary>
	/// Compresses the lzma.
	/// </summary>
    private static void CompressLzma()
    {
        if(resourcesLis == null || resourcesLis.Count <= 0)
        {
            return;
        }

        string assetBundlePath = Application.dataPath + "/AssetBundle";
        string lzmaFilePath = assetBundlePath + "/AssetBundle.lzma";
        if(!Directory.Exists(assetBundlePath))
        {
            Directory.CreateDirectory(assetBundlePath);
        }

        try
        {
            MemoryStream memoryStream = new MemoryStream();
            FileStream compressStream = new FileStream(lzmaFilePath, FileMode.OpenOrCreate, FileAccess.Write);

            int lastIndex = Application.dataPath.LastIndexOf("/");
            string prePath = Application.dataPath.Substring(0, lastIndex + 1);
            int filePathCount = resourcesLis.Count;
            for (int i = 0; i < filePathCount; i++)
            {
                string assetPath = AssetDatabase.GetAssetPath(resourcesLis[i]);
                string filePath = prePath + assetPath;
                string zipBundlePath = assetPath.Replace("Assets/", "");

                FileStream tempFileStream = File.Open(filePath, FileMode.Open);

                StringBuilder sb = new StringBuilder(); // set header info: path + filesie + separator
                sb.Append(zipBundlePath).Append(",").Append(tempFileStream.Length).Append("\n");

                byte[] tempBuff = new byte[tempFileStream.Length];
                byte[] header = Encoding.UTF8.GetBytes(sb.ToString());
                tempFileStream.Read(tempBuff, 0, (int)tempFileStream.Length);     // get file data

                memoryStream.Write(header, 0, header.Length);
                memoryStream.Write(tempBuff, 0, tempBuff.Length);

                tempFileStream.Close();
            }

            // important !!!
            memoryStream.Position = 0;

            SevenZip.Compression.LZMA.Encoder encoder = new SevenZip.Compression.LZMA.Encoder();

            encoder.WriteCoderProperties(compressStream);

            byte[] compressLen = new byte[8];  // file size
            for (int i = 0; i < compressLen.Length; i++)
            {
                compressLen[i] = (byte)(memoryStream.Length >> (8 * i));
            }
            compressStream.Write(compressLen, 0, 8);

            CodeProgress codeProgress = new CodeProgress(); // compress
            codeProgress.totalSize = memoryStream.Length;
            encoder.Code(memoryStream, compressStream, memoryStream.Length, -1, codeProgress);

            memoryStream.Flush();
            memoryStream.Close();
            compressStream.Close();

            AssetDatabase.Refresh(); // refresh asssets
            EditorUtility.ClearProgressBar();
        }
        catch (Exception exe)
        {
            Debug.Log(exe.Message);
        }
    }


    /// <summary>
    /// decompress lzma file
    /// </summary>
    private static void DecompressLzma()
    {
        string assetbundlePath = Application.dataPath + "/AssetBundle";
        string destFolderPath = assetbundlePath + "/AssetBundle.lzma";

        FileStream compressStream = new FileStream(destFolderPath, FileMode.Open, FileAccess.Read);
        MemoryStream tempStream = new MemoryStream();

        SevenZip.Compression.LZMA.Decoder decoder = new SevenZip.Compression.LZMA.Decoder();

        byte[] properties = new byte[5];
        compressStream.Read(properties, 0, 5);
        decoder.SetDecoderProperties(properties);

        byte[] compressLen = new byte[8];
        compressStream.Read(compressLen, 0, 8);
        long outsize = 0;
        for (int i = 0; i < 8; i++)
        {
            outsize |= (long)((byte)compressLen[i] << (8 * i));
        }

        long compressRealLen = compressStream.Length - compressStream.Position;
        decoder.Code(compressStream, tempStream, compressRealLen, outsize, null);

        // importtant !!!
        tempStream.Position = 0;

        try
        {
            byte[] head = new byte[1024];
            int index = 0;
            while (tempStream.Position != tempStream.Length)
            {
                int one = tempStream.ReadByte();
                if (one != 10) // separator: \n
                {
                    head[index++] = (byte)one;
                }
                else
                {
                    index = 0;
                    string headStr = Encoding.UTF8.GetString(head);
                    head = new byte[1024];

                    string[] headArr = headStr.Split(',');
                    string fileBundleName = headArr[0];
                    int lastXIndex = fileBundleName.LastIndexOf("/");
                    string fileName = fileBundleName.Substring(lastXIndex + 1, fileBundleName.Length - lastXIndex - 1);
                    string fileFolderName = fileBundleName.Substring(0, lastXIndex);

                    int fileSize = 0;
                    if(!int.TryParse(headArr[1], out fileSize))
                    {
                        EditorUtility.DisplayDialog("Error", "File size exception!!! -->> " + headArr[1], "ok");
                        break;
                    }

                    string filePath = assetbundlePath + "/Decompress/" + fileFolderName;
                    if (!Directory.Exists(filePath))
                    {
                        Directory.CreateDirectory(filePath);
                    }

                    WriteToLocal(tempStream, fileSize, filePath + "/" + fileName);
                }
            }

            tempStream.Flush();
            tempStream.Close();

            AssetDatabase.Refresh();
        }
        catch (Exception exe)
        {
            Debug.Log(exe.Message);
        }
    }

    /// <summary>
    /// decompress to corresponding folder
    /// </summary>
    private static void WriteToLocal(MemoryStream file, int fileLen, string intoPath)
    {
        FileStream output = new FileStream(intoPath, FileMode.Create, FileAccess.Write);
        byte[] temp = new byte[fileLen];

        int readResult = file.Read(temp, 0, fileLen);
        output.Write(temp, 0, readResult);

        output.Flush();
        output.Close();
    }

    /// <summary>
    /// call back
    /// </summary>
    public class CodeProgress : SevenZip.ICodeProgress
    {
        // bytes
        public long totalSize { get; set; }

        public void SetProgress(Int64 inSize, Int64 outSize)  //show progress
        {
            EditorUtility.DisplayProgressBar("LZMA", "Compress or decompress", (float)inSize / totalSize);
        }
    }



}
