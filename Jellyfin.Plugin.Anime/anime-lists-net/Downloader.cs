using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AnimeLists
{
    public class Downloader
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly string _temp;

        public Downloader(string temp)
        {
            _temp = temp;
        }

        public async Task<Animelist> DownloadAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var info = new FileInfo(_temp);
                if (!info.Exists || info.LastWriteTimeUtc < (DateTime.UtcNow - TimeSpan.FromDays(7)))
                {
                    if (info.Exists)
                    {
                        info.Delete();
                    }

                    using (HttpClient client = new HttpClient())
                    using (Stream str = await client
                        .GetStreamAsync("https://raw.githubusercontent.com/ScudLee/anime-lists/master/anime-list.xml").ConfigureAwait(false))
                    using (FileStream file = new FileStream(_temp, FileMode.CreateNew))
                    {
                        await str.CopyToAsync(file).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _lock.Release();
            }

            XmlSerializer serializer = new XmlSerializer(typeof(Animelist));

            using (var stream = File.OpenRead(_temp))
            {
                return serializer.Deserialize(stream) as Animelist;
            }
        }
    }
}
