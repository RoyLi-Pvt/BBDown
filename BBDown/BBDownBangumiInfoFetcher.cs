using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Text;
using static BBDown.BBDownEntity;
using static BBDown.BBDownUtil;

namespace BBDown
{
    class BBDownBangumiInfoFetcher : IFetcher
    {
        public BBDownVInfo Fetch(string id)
        {
            id = id.Substring(3);
            string index = "";
            string api = $"https://api.bilibili.com/pgc/view/web/season?ep_id={id}";
            string json = GetWebSource(api);
            JsonDocument infoJson = JsonDocument.Parse(json);
            string cover = infoJson.RootElement.GetProperty("result").GetProperty("cover").GetString();
            string title = infoJson.RootElement.GetProperty("result").GetProperty("title").GetString();
            string desc = infoJson.RootElement.GetProperty("result").GetProperty("evaluate").GetString();
            string pubTime = infoJson.RootElement.GetProperty("result").GetProperty("publish").GetProperty("pub_time").GetString();
            JsonElement pages = infoJson.RootElement.GetProperty("result").GetProperty("episodes");//JArray.Parse(infoJson.RootElement.GetProperty("result"]["episodes"].ToString());
            List<Page> pagesInfo = new List<Page>();
            int i = 1;

            //episodes为空; 或者未包含对应epid，番外/花絮什么的
            if (pages.GetArrayLength() == 0 || !pages.ToString().Contains($"/ep{id}")) 
            {
                if (infoJson.RootElement.GetProperty("result").TryGetProperty("section",out var Section))
                {
                    foreach (var Sec in Section.EnumerateArray())
                    {
                        if (Sec.ToString().Contains($"/ep{id}"))
                        {
                            title += "[" + Sec.GetProperty("title").GetString() + "]";
                            pages = Section.GetProperty("episodes");
                            break;
                        }
                    }
                }
            }

            foreach (var page in pages.EnumerateArray())
            {
                //跳过预告
                if (page.TryGetProperty("badge",out var Badge) && Badge.GetString() == "预告") continue;
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
