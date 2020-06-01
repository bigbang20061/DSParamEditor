﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace UXM
{
    class ExePatcher
    {
        private static readonly Encoding UTF16 = Encoding.Unicode;

        public static string Patch(string exePath, IProgress<(double value, string status)> progress, CancellationToken ct)
        {
            progress.Report((0, "Preparing to patch..."));
            string gameDir = Path.GetDirectoryName(exePath);
            string exeName = Path.GetFileName(exePath);

            Util.Game game;
            GameInfo gameInfo;
            try
            {
                game = Util.GetExeVersion(exePath);
                gameInfo = GameInfo.GetGameInfo(game);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            if (game == Util.Game.Sekiro)
            {
                DialogResult choice = MessageBox.Show("对于Sekiro，大多数用户应该使用Mod引擎，而不是使用UXM补丁。修补启动游戏的exe文件将导致游戏在启动时崩溃。\n" +
                    "你确定要修补吗？", "注意", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (choice == DialogResult.No)
                {
                    progress.Report((1, "Patching cancelled."));
                    return null;
                }
            }

            if (!File.Exists(gameDir + "\\_backup\\" + exeName))
            {
                try
                {
                    Directory.CreateDirectory(gameDir + "\\_backup");
                    File.Copy(exePath, gameDir + "\\_backup\\" + exeName);
                }
                catch (Exception ex)
                {
                    return $"Failed to backup file:\r\n{exePath}\r\n\r\n{ex}";
                }
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(exePath);
            }
            catch (Exception ex)
            {
                return $"Failed to read file:\r\n{exePath}\r\n\r\n{ex}";
            }

            try
            {
                for (int i = 0; i < gameInfo.Replacements.Count; i++)
                {
                    if (ct.IsCancellationRequested)
                        return null;

                    string target = gameInfo.Replacements[i];
                    string replacement = "." + new string('/', target.Length - 1);

                    // Add 1.0 for preparation step
                    progress.Report(((i + 1.0) / (gameInfo.Replacements.Count + 1.0), $"Patching alias \"{target}\" ({i + 1}/{gameInfo.Replacements.Count})..."));

                    replace(bytes, target, replacement);
                }
            }
            catch (Exception ex)
            {
                return $"Failed to patch file:\r\n{exePath}\r\n\r\n{ex}";
            }

            try
            {
                File.WriteAllBytes(exePath, bytes);
            }
            catch (Exception ex)
            {
                return $"Failed to write file:\r\n{exePath}\r\n\r\n{ex}";
            }

            progress.Report((1, "Patching complete!"));
            return null;
        }

        private static void replace(byte[] bytes, string target, string replacement)
        {
            byte[] targetBytes = UTF16.GetBytes(target);
            byte[] replacementBytes = UTF16.GetBytes(replacement);
            if (targetBytes.Length != replacementBytes.Length)
                throw new ArgumentException($"Target length: {targetBytes.Length} | Replacement length: {replacementBytes.Length}");

            List<int> offsets = findBytes(bytes, targetBytes);
            foreach (int offset in offsets)
                Array.Copy(replacementBytes, 0, bytes, offset, replacementBytes.Length);
        }

        private static List<int> findBytes(byte[] bytes, byte[] find)
        {
            List<int> offsets = new List<int>();
            for (int i = 0; i < bytes.Length - find.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < find.Length; j++)
                {
                    if (find[j] != bytes[i + j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                    offsets.Add(i);
            }
            return offsets;
        }
    }
}
