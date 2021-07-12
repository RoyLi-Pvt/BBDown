using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Text;
using static BBDown.BBDownEntity;
using static BBDown.BBDownUtil;
using System.Linq;

namespace BBDown
{
    class BBDownCheeseInfoFetcher : IFetcher
    {
        public BBDownVInfo Fetch(string id)
        {
            id = id.Substring(7);
            string index = "";
            string api = $"https://api.bilibili.com/pugv/view/web/season?ep_id={id}";
            string json = GetWebSource(api);
            JsonDocument infoJson = JsonDocument.Parse(json);
            string cover = infoJson.RootElement.GetProperty("data").GetProperty("cover").GetString();
            string title = infoJson.RootElement.GetProperty("data").GetProperty("title").GetString();
            string desc = infoJson.RootElement.GetProperty("data").GetProperty("subtitle").GetString();
            var pages = infoJson.RootElement.GetProperty("data").GetProperty("episodes");
            List<Page> pagesInfo = new List<Page>();
            foreach (var page in pages.EnumerateArray())
            {
                Page p = new Page(page.GetProperty("index").GetInt32(),
                    page.GetProperty("aid").GetString(),
                    page.GetProperty("cid").GetString(),
                    page.GetProperty("id").GetString(),
                    page.GetProperty("title").GetString().Trim(),
                    page.GetProperty("duration").GetInt32(), "");
                if (p.epid == id) index = p.index.ToString();
                pagesInfo.Add(p);
            }
            string pubTime = pagesInfo.Count > 0 ? pages.EnumerateArray().First().GetProperty("release_date").GetString() : "";
            pubTime = pubTime != "" ? (new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Convert.ToDouble(pubTime)).ToLocalTime().ToString()) : "";

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
