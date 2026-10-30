using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// ビルドの前に、ビルドした日時とgitのコミットを Assets/Resources/build_info.txt に書く（README 6.3）
// ログの session_start に build=… built=… として残り、どのビルドで遊んだかが分かる
// このファイルはgitに入れない（.gitignore の Assets/Resources/*.txt）
public class BuildInfoWriter : IPreprocessBuildWithReport
{
    private const string FilePath = "Assets/Resources/build_info.txt";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Write();
    }

    public static string Write()
    {
        // コミットしていない変更があれば -dirty（その版はgitに残っていない）。gitが使えなければ unknown
        string commit = Git("rev-parse --short HEAD");
        if (commit == null)
            commit = "unknown";
        else if (!string.IsNullOrEmpty(Git("status --porcelain")))
            commit += "-dirty";

        string info = "build=" + commit + ";built=" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        File.WriteAllText(FilePath, info);
        AssetDatabase.ImportAsset(FilePath);

        UnityEngine.Debug.Log("[Build] " + info);
        return info;
    }

    // プロジェクトのフォルダで git を実行し、出力を返す。失敗したら null
    private static string Git(string args)
    {
        try
        {
            var start = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(start);
            string output = process.StandardOutput.ReadToEnd().Trim();
            if (!process.WaitForExit(5000) || process.ExitCode != 0)
                return null;
            return output;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
