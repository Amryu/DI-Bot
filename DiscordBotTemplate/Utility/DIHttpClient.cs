using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DIBot
{
    class DIHttpClient : HttpClient
    {

        public DIHttpClient(HttpClientHandler handler) : base(handler)
        {
            BaseAddress = new Uri("https://di.community/");

            SetChromeHeaders();
        }

        public static DIHttpClient CreateWithAuthCookies(List<Cookie> cookies)
        {
            var handler = new HttpClientHandler();

            handler.CookieContainer = new CookieContainer();

            cookies
                .Select(x => new System.Net.Cookie(x.Name, x.Value, x.Path, x.Domain))
                .ToList()
                .ForEach(x => handler.CookieContainer.Add(x));

            return new DIHttpClient(handler);
        }

        public async Task<string> GetCalendarAsync(string calendar)
        {
            return await GetStringAsync($"calendar/{calendar}/download/?member={ConfigUtil.Config.AuthConfig.MemberId}&key={ConfigUtil.Config.AuthConfig.MemberKey}");
        }

        public async Task<List<string>> GetEventRsvpAsync(string eventName)
        {
            try
            {
                var eventDoc = new HtmlDocument();
                
                eventDoc.LoadHtml(await GetStringAsync($"calendar/event/{eventName}/"));

                var rsvpUrl = eventDoc.DocumentNode.SelectSingleNode($"//a[contains(@href,'https://di.community/calendar/event/{eventName}/?do=downloadRsvp')]")
                    .Attributes["href"]
                    .Value
                    .Replace("&amp;", "&")
                    .Replace("https://di.community/", "");

                var eventRsvpDoc = new HtmlDocument();

                eventRsvpDoc.LoadHtml(await GetStringAsync(rsvpUrl));

                return eventRsvpDoc.DocumentNode.SelectNodes("//li").Select(x => x.InnerText).ToList();
            }
            catch(Exception e)
            {
                e.ToString();
            }

            return null;
        }

        private void SetChromeHeaders()
        {
            DefaultRequestHeaders.Accept.Clear();
            DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
            DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
            DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/apng"));
            DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));

            DefaultRequestHeaders.AcceptEncoding.Clear();
            //DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            //DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            //DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

            DefaultRequestHeaders.AcceptLanguage.Clear();
            DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("de-DE"));
            DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("de", 0.9));
            DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 0.8));
            DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.7));

            DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue();
            DefaultRequestHeaders.CacheControl.NoCache = true;

            DefaultRequestHeaders.Pragma.Clear();
            DefaultRequestHeaders.Pragma.Add(new NameValueHeaderValue("no-cache"));

            DefaultRequestHeaders.Referrer = new Uri($"{BaseAddress}76-league-of-legends/");

            DefaultRequestHeaders.UserAgent.Clear();
            DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
            DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AppleWebKit", "537.36"));
            DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Chrome", "69.0.3497.92"));
            DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Safari", "537.36"));
        }
    }
}
