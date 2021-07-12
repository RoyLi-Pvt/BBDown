using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using static BBDown.BBDownEntity;
using static BBDown.BBDownUtil;

namespace BBDown
{
    class BBDownIntlBangumiInfoFetcher : IFetcher
    {
        public BBDownVInfo Fetch(string id)
        {
            id = id.Substring(3);
            string index = "";
            //string api = $"https://api.global.bilibili.com/intl/gateway/ogv/m/view?ep_id={id}&s_locale=ja_JP";
            string api = $"https://api.global.bilibili.com/intl/gateway/v2/ogv/view/app/season?ep_id={id}&platform=android&s_locale=zh_SG&mobi_app=bstar_a" + (Program.TOKEN != "" ? $"&access_key={Program.TOKEN}" : "");
            string json = GetWebSource(api);
            var infoJson = JsonDocument.Parse(json);
            string seasonId = infoJson.RootElement.GetProperty("result").GetProperty("season_id").GetString();
            string cover = infoJson.RootElement.GetProperty("result").GetProperty("cover").GetString();
            string title = infoJson.RootElement.GetProperty("result").GetProperty("title").GetString();
            string desc = infoJson.RootElement.GetProperty("result").GetProperty("evaluate").GetString();


            if (cover == "")
            {
                string animeUrl = $"https://bangumi.bilibili.com/anime/{seasonId}";
                var web = GetWebSource(animeUrl);
                if (web != "")
                {
                    Regex regex = new Regex("window.__INITIAL_STATE__=([\\s\\S].*?);\\(function\\(\\)");
                    string _json = regex.Match(web).Groups[1].Value;
                    var Json = JsonDocument.Parse(_json);
                    cover = Json.RootElement.GetProperty("mediaInfo").GetProperty("cover").GetString();
                    title = Json.RootElement.GetProperty("mediaInfo").GetProperty("title").GetString();
                    desc = Json.RootElement.GetProperty("mediaInfo").GetProperty("evaluate").GetString();
                }
            }

            string pubTime = infoJson.RootElement.GetProperty("result").GetProperty("publish").GetProperty("pub_time").GetString();
            infoJson.RootElement.GetProperty("result").TryGetProperty("episodes",out var pages);
            List<Page> pagesInfo = new List<Page>();
            int i = 1;

            if (infoJson.RootElement.GetProperty("result").GetProperty("modules").ValueKind != JsonValueKind.Null)
            {
                foreach (var section in infoJson.RootElement.GetProperty("result").GetProperty("modules").EnumerateArray())
                {
                    if (section.ToString().Contains($"/{id}"))
                    {
                        pages = section.GetProperty("data").GetProperty("episodes");
                        break;
                    }
                }
            }

            /*if (pages.Count == 0)
            {
                if (web != "") 
                {
                    string epApi = $"https://api.bilibili.com/pgc/web/season/section?season_id={seasonId}";
                    var _web = GetWebSource(epApi);
                    pages = JArray.Parse(JsonDocument.Parse(_web)["result"]["main_section"]["episodes"].ToString());
                }
                else if (infoJson.RootElement.GetProperty("data"]["modules"] != null)
                {
                    foreach (JsonDocument section in JArray.Parse(infoJson.RootElement.GetProperty("data"]["modules"].ToString()))
                    {
                        if (section.ToString().Contains($"ep_id={id}"))
                        {
                            pages = JArray.Parse(section["data"]["episodes"].ToString());
                            break;
                        }
                    }
                }
            }*/

            foreach (var page in pages.EnumerateArray())
            {
                //跳过预告
                if (page.TryGetProperty("badge",out var badge) && badge.ToString() == "预告") continue;
                string res = "";
                try
                {
                    res = page.GetProperty("dimension").GetProperty("width").GetString() + "x" + page.GetProperty("dimension").GetProperty("height").GetString();
                }
                catch (Exception) { }
                string _title = page.GetProperty("title").GetString() + " " + page.GetProperty("long_title").GetString().Trim();
                _title = _title.Trim();
                Page p = new Page(i++,
                    page.GetProperty("aid").GetString(),
                    page.GetProperty("cid").GetString(),
                    page.GetProperty("id").GetString(),
                    _title,
                    0, res);
                if (p.epid == id) index = p.index.ToString();
                pagesInfo.Add(p);
            }


            var info = new BBDownVInfo();
            info.Title = title.Trim();
            info.Desc = desc.Trim();
            info.Pic = cover;
            info.PubTime = pubTime;
            info.PagesInfo = pagesInfo;
            info.IsBangumi = true;
            info.IsCheese = true;
            info.Index = index;

            return info;
        }
    }
}
