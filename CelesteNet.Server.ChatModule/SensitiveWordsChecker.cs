using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CelesteNet.Server.Chat {
    using System;
    using System.IO;
    using System.Net;

    class SensitiveWordsChecker {
        private const string LocalFilePath = "sensitive_words.txt";
        private const string RemoteFileUrl = "https://raw.githubusercontent.com/observerss/textfilter/0b9269eb002fb19e5ac5f6022f6d1e103bb122f8/keywords";
        public delegate string CheckSentence(string sentence);
        public static CheckSentence InitChecker() {
            if (!File.Exists(LocalFilePath)) {
                Console.WriteLine("本地无词库，正在从 GitHub 下载敏感词库");
                DownloadFile(RemoteFileUrl, LocalFilePath);
            }

            Console.WriteLine("正在加载敏感词库");

            string[] sensitiveWords = LoadSensitiveWords(LocalFilePath);

            return origin => HideForbiddenWords(origin, sensitiveWords);
        }

        static void DownloadFile(string url, string savePath) {
            using (WebClient client = new WebClient()) {
                client.DownloadFile(url, savePath);
            }
        }

        static string[] LoadSensitiveWords(string filePath) {
            string fileContent = File.ReadAllText(filePath);
            return fileContent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        }

        static string HideForbiddenWords(string original, string[] sensitiveWords) {
            foreach (string word in sensitiveWords) {
                original = original.Replace(word, "●".PadRight(word.Length, '●'));
            }

            return original;
        }
    }
}
