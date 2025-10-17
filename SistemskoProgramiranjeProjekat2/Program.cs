using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

//Kreirati Web server koji vrši brojanje reči u okviru fajla. Brojati samo reči koje imaju vise suglasnika nego
//samoglanika. Svi zahtevi serveru se šalju preko browser-a korišćenjem GET metode. U zahtevu se kao
//parametar navodi naziv fajla. Server prihvata zahtev, pretražuje root folder i sve njegove podfoldere za
//zahtevani fajl i vrši brojanje. Ukoliko traženi fajl ne postoji, vratiti grešku korisniku. Takođe, ukoliko nema
//takvih reči, vratiti odgovarajuću poruku korisniku.
//Primer poziva serveru: http://localhost:5050/fajl.txt

namespace SistemskoProgramiranjeProjekat2
{
    internal class Program
    {
        private static ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>();
        private static object cacheLock = new object();

        private static string rootPath = AppDomain.CurrentDomain.BaseDirectory;
        
        static void Main(string[] args)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5050/");
            listener.Start();

            Console.WriteLine("Pokrenut server na: http://localhost:5050/");

            while(true)
            {
                HttpListenerContext context = listener.GetContext();

                Task.Run(() => ObradiZahtev(context));
            }
        }
        private static async void ObradiZahtev(HttpListenerContext context)
        {
            string fileName = context.Request.RawUrl.TrimStart('/');

            Console.WriteLine($"\n[Zahtev] Primljen zahtev za fajl: {fileName}");


            string responseText = "";

            lock (cacheLock)// samo jedan thread moze da pristupa kesu u datom trenutku
            {
                if (cache.TryGetValue(fileName, out responseText))
                {
                    Console.WriteLine("[Keš] Poslat rezultat iz kesa.");
                    string response = $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><title>My Web Page</title></head><body><h1>{responseText}</h1></body></html>";
                    Odgovori(context, response);
                    return;
                }
            }

            // Traženje fajla u root-u i podfolderima
            string filePath = PronadjiFajl(rootPath, fileName);

            if (filePath == null)
            {
                responseText = $"Greska: fajl \"{fileName}\" nije pronadjen.";
                Console.WriteLine("[Greska] " + responseText);
                string response = $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><title>My Web Page</title></head><body><h1>{responseText}</h1></body></html>";
                Odgovori(context, response, 404);
                return;
            }

            try
            {
                using (StreamReader sr = new StreamReader(filePath, Encoding.UTF8))
                {
                    string txt;
                    string[] words;
                    int count = 0;
                    txt = await sr.ReadToEndAsync();
                    words = txt.Split(new char[] { ' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?' });
                    foreach(var word in words)
                    {
                        if (ImaViseSuglasnika(word))
                            count++;
                    }

                    if (count > 0)
                        responseText = $"Broj reci sa vise suglasnika nego samoglasnika u fajlu \"{fileName}\" je: {count}.";
                    else
                        responseText = $"Fajl nema reci koje imaju vise suglasnika nego samoglasnika.";
                    Console.WriteLine("[Uspeh] " + responseText);

                }

                // Dodaj u keš
                lock (cacheLock)
                {
                    cache[fileName] = responseText;
                }
                string response = $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><title>My Web Page</title></head><body><h1>{responseText}</h1></body></html>";
                Odgovori(context, response);
            }
            catch (Exception ex)
            {
                responseText = "Greska prilikom obrade fajla: " + ex.Message;
                Console.WriteLine("[Greska] " + responseText);
                string response = $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><title>My Web Page</title></head><body><h1>{responseText}</h1></body></html>";
                Odgovori(context, response, 500);
            }
        }

        private static string PronadjiFajl(string folder, string fileName)
        {
            try
            {
                string ret = Directory.GetFiles(folder, fileName, SearchOption.AllDirectories)[0];
                if (ret != null)
                    return ret;
            }
            catch { }
            return null;
        }

        private static bool ImaViseSuglasnika(string word)
        {
            int samoglasnici = 0, suglasnici = 0;
            string s = word.ToLower();

            foreach (char c in s)
            {
                if (char.IsLetter(c))
                {
                    if ("aeiouAEIOU".Contains(c))
                        samoglasnici++;
                    else
                        suglasnici++;
                }
            }
            return suglasnici > samoglasnici;
        }

        private static async void Odgovori(HttpListenerContext context, string responseText, int statusCode = 200)
        {
            context.Response.StatusCode = statusCode;
            byte[] buffer = Encoding.UTF8.GetBytes(responseText);
            context.Response.ContentLength64 = buffer.Length;
            using (Stream output = context.Response.OutputStream)
            {
                await output.WriteAsync(buffer, 0, buffer.Length);
            }
        }
    }
}
