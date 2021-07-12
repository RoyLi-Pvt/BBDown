using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using static BBDown.BBDownEntity;
using static BBDown.BBDownUtil;

namespace BBDown
{
    class BBDownNormalInfoFetcher : IFetcher
    {
        public BBDownVInfo Fetch(string id)
        {
            string api = $"https://api.bilibili.com/x/web-interface/view?aid={id}";
            string json = GetWebSource(api);
            JsonDocument infoJson = JsonDocument.Parse(json);
            string title = infoJson.RootElement.GetProperty("data").GetProperty("title").GetString();
            string desc = infoJson.RootElement.GetProperty("data").GetProperty("desc").GetString();
            string pic = infoJson.RootElement.GetProperty("data").GetProperty("pic").GetString();
            string pubTime = infoJson.RootElement.GetProperty("data").GetProperty("pubdate").ToString();
            pubTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Convert.ToDouble(pubTime)).ToLocalTime().ToString();
            bool bangumi = false;

            var pages = infoJson.RootElement.GetProperty("data").GetProperty("pages");
            List<Page> pagesInfo = new List<Page>();
            foreach (var page in pages.EnumerateArray())
            {
                Page p = new Page(page.GetProperty("page").GetInt32(),
                    id,
                    page.GetProperty("cid").ToString(),
                    "", //epid
                    page.GetProperty("part").GetString().Trim(),
                    page.GetProperty("duration").GetInt32(),
                    page.GetProperty("dimension").GetProperty("width").ToString() + "x" + page.GetProperty("dimension").GetProperty("height").ToString());
                pagesInfo.Add(p);
            }

            try
            {
                if (infoJson.RootElement.GetProperty("data").GetProperty("redirect_url").GetString().Contains("bangumi"))
                {
                    bangumi = true;
                    string epId = Regex.Match(infoJson.RootElement.GetProperty("data").GetProperty("redirect_url").GetString(), "ep(\\d+)").Groups[1].Value;
                    //番剧内容通常不会有分P，如果有分P则不需要epId参数
                    if (pages.GetArrayLength() == 1)
                    {
                        pagesInfo.ForEach(p => p.epid = epId);
                    }
                }
            }
            catch { }

            var info = new BBDownVInfo();
            info.Title = title.Trim();
            info.Desc = desc.Trim();
            info.Pic = pic;
            info.PubTime = pubTime;
            info.PagesInfo = pagesInfo;
            info.IsBangumi = bangumi;

            return info;
        }
    }
}
