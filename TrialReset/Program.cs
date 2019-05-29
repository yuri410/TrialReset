using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace TrialReset
{
    struct RegistryCandidate
    {
        public int score;
        public RegistryKey key;

        public RegistryCandidate(int score, RegistryKey key)
        {
            this.score = score;
            this.key = key;
        }
    }

    class RegistryCandidateList
    {
        List<RegistryCandidate> candidates = new List<RegistryCandidate>();

        public void Position(int score, RegistryKey key)
        {
            if (candidates.Count < 10)
            {
                candidates.Add(new RegistryCandidate(score, key));
            }
            else
            {
                int minScore = int.MaxValue;
                int minIdx = -1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i].score < minScore)
                    {
                        minScore = candidates[i].score;
                        minIdx = i;
                    }
                }
                if (minIdx != -1)
                {
                    candidates[minIdx] = new RegistryCandidate(score, key);
                }
            }
        }

        public RegistryKey[] GetCandidates()
        {
            RegistryKey[] r = new RegistryKey[candidates.Count];
            int pos = 0;

            foreach (RegistryCandidate c in candidates)
            {
                r[pos++] = c.key;
            }
            return r;
        }
    }

    class Program
    {
        const string symbols = "-_+=[]{}`~;':\",.<>!@#$%^&*()";

        static bool dryRun = true;

        static int CountSymbol(string str)
        {
            int count = 0;
            for (int i = 0; i < str.Length; i++)
            {
                if (symbols.IndexOf(str[i]) != -1)
                {
                    count++;
                }
            }
            return count;
        }

        static bool HasAnyWord(string[] dict, string str)
        {
            for (int i = 0; i < dict.Length; i++)
            {
                if (str.Contains(dict[i]))
                {
                    return true;
                }
            }
            return false;
        }
        

        static string[] LoadDictionary()
        {
            List<string> dict = new List<string>();

            Stream strm = Assembly.GetEntryAssembly().GetManifestResourceStream("TrialReset.words_alpha.txt");
            
            using (StreamReader sr = new StreamReader(strm))
            {
                while (!sr.EndOfStream)
                    dict.Add(sr.ReadLine());
            }

            return dict.ToArray();
        }

        static void RemoveLicenseInRegistry()
        {
            string[] dict = LoadDictionary();

            Console.Write("Scanning: ");
            int curLeft = Console.CursorLeft;
            int curTop = Console.CursorTop;

            RegistryCandidateList candidates = new RegistryCandidateList();

            RegistryKey clsid = Registry.ClassesRoot.OpenSubKey("Wow6432Node", true).OpenSubKey("CLSID", true);

            int printCounter = 0;
            foreach (string id in clsid.GetSubKeyNames())
            {
                if ((++printCounter % 16) == 0)
                {
                    Console.CursorLeft = curLeft;
                    Console.CursorTop = curTop;

                    Console.Write(id);
                }

                RegistryKey key = clsid.OpenSubKey(id);

                if (key.SubKeyCount < 8)
                    continue;

                int scrambledCount = 0;
                int symbolCount = 0;

                foreach (string member in key.GetSubKeyNames())
                {
                    try
                    {
                        var fld = key.OpenSubKey(member);

                        if (fld.ValueCount == 1 && fld.SubKeyCount == 0 &&
                            fld.GetValueKind(null) == RegistryValueKind.String)
                        {
                            string data = (string)fld.GetValue(null);

                            symbolCount += CountSymbol(data);

                            if (!HasAnyWord(dict, member))
                                scrambledCount++;
                        }
                    }
                    catch { }
                }

                if (scrambledCount > 0)
                {
                    int score = scrambledCount + symbolCount;
                    candidates.Position(score, key);
                }
            }

            Console.WriteLine();

            foreach (RegistryKey key in candidates.GetCandidates())
            {
                Console.WriteLine(key.Name);

                foreach (string member in key.GetSubKeyNames())
                {
                    Console.Write("  ");
                    Console.WriteLine(member);
                }

                Console.Write("Remove this? Confirm with (Y/n): ");

                if (Console.ReadKey().Key == ConsoleKey.Y)
                {
                    if (!dryRun)
                        clsid.DeleteSubKeyTree(key.Name);
                }

                Console.WriteLine();
            }

            try
            {
                Console.WriteLine("Removing Licenses registry key.");

                if (!dryRun)
                    Registry.CurrentUser.OpenSubKey("Software", true).DeleteSubKey("Licenses");
            }
            catch
            {
                Console.WriteLine("Can't remove registry key.");
            }
        }

        static void RemoveLicenseFile()
        {
            try
            {
                Console.WriteLine("Removing temp file.");

                if (!dryRun)
                {
                    string filename = new string(new char[] { '1', '4', '8', '9', 'A', 'F', 'E', '4', '.', 'T', 'M', 'P' });

                    File.Delete(Path.Combine(Path.GetTempPath(), filename));
                }
            }
            catch
            {
                Console.WriteLine("Can't remove temp file.");
            }
        }

        static bool CheckDevenv()
        {
            return Process.GetProcessesByName("devenv").Length > 0;
        }

        static void Main(string[] args)
        {
            if (CheckDevenv())
            {
                Console.WriteLine("Please close Visual Studio and try again.");
            }
            else
            {
                RemoveLicenseInRegistry();
                RemoveLicenseFile();
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
