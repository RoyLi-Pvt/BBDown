using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using static BBDown.BBDownUtil;
using static BBDown.BBDownLogger;
using static BBDown.BBDownEntity;
using System.Text.Json;
using System.Linq;

namespace BBDown
{
    class BBDownParser
    {
        private static string GetPlayJson(bool onlyAvc, string aidOri, string aid, string cid, string epId, bool tvApi, bool intl, bool appApi, string qn = "0")
        {
            LogDebug("aid={0},cid={1},epId={2},tvApi={3},IntlApi={4},appApi={5},qn={6}", aid, cid, epId, tvApi, intl, appApi, qn);

            if (intl) return GetPlayJson(aid, cid, epId, qn);


            bool cheese = aidOri.StartsWith("cheese:");
            bool bangumi = cheese || aidOri.StartsWith("ep:");
            LogDebug("bangumi={0},cheese={1}", bangumi, cheese);

            if (appApi) return BBDownAppHelper.DoReq(aid, cid, qn, bangumi, onlyAvc, Program.TOKEN);

            string prefix = tvApi ? (bangumi ? "api.snm0516.aisee.tv/pgc/player/api/playurltv" : "api.snm0516.aisee.tv/x/tv/ugc/playurl")
                        : (bangumi ? "api.bilibili.com/pgc/player/web/playurl" : "api.bilibili.com/x/player/playurl");
            string api = $"https://{prefix}?avid={aid}&cid={cid}&qn={qn}&type=&otype=json" + (tvApi ? "" : "&fourk=1") +
                $"&fnver=0&fnval=80" + (tvApi ? "&device=android&platform=android" +
                "&mobi_app=android_tv_yst&npcybs=0&force_host=0&build=102801" +
                (Program.TOKEN != "" ? $"&access_key={Program.TOKEN}" : "") : "") +
                (bangumi ? $"&module=bangumi&ep_id={epId}&fourk=1" + "&session=" : "");
            if (tvApi && bangumi)
            {
                api = (Program.TOKEN != "" ? $"access_key={Program.TOKEN}&" : "") +
                    $"aid={aid}&appkey=4409e2ce8ffd12b8&build=102801" +
                    $"&cid={cid}&device=android&ep_id={epId}&expire=0" +
                    $"&fnval=80&fnver=0&fourk=1" +
                    $"&mid=0&mobi_app=android_tv_yst" +
                    $"&module=bangumi&npcybs=0&otype=json&platform=android" +
                    $"&qn={qn}&ts={GetTimeStamp(true)}";
                api = $"https://{prefix}?" + api + (bangumi ? $"&sign={GetSign(api)}" : "");
            }

            //课程接口
            if (cheese) api = api.Replace("/pgc/", "/pugv/");

            //Console.WriteLine(api);
            string webJson = GetWebSource(api);
            //以下情况从网页源代码尝试解析
            if (webJson.Contains("\"大会员专享限制\""))
            {
                string webUrl = "https://www.bilibili.com/bangumi/play/ep" + epId;
                string webSource = GetWebSource(webUrl);
                webJson = Regex.Match(webSource, @"window.__playinfo__=([\s\S]*?)<\/script>").Groups[1].Value;
            }
            return webJson;
        }

        private static string GetPlayJson(string aid, string cid, string epId, string qn)
        {
            string api = $"https://api.global.bilibili.com/intl/gateway/v2/ogv/playurl?" +
                $"aid={aid}&appkey=7d089525d3611b1c&build=1000310&c_locale=&channel=master&cid={cid}&ep_id={epId}&force_host=0&fnvalfnval=80&fnver=0&fourk=1&lang=hans&locale=zh_CN&mobi_app=bstar_a&platform=android&prefer_code_type=0&qn={qn}&timezone=GMT%2B08%3A00&ts={GetTimeStamp(true)}" + (Program.TOKEN != "" ? $"&access_key={Program.TOKEN}" : "");
            string webJson = GetWebSource(api);
            return webJson;
        }

        public static (string, List<Video>, List<Audio>, List<string>, List<string>) ExtractTracks(bool onlyHevc, bool onlyAvc, string aidOri, string aid, string cid, string epId, bool tvApi, bool intlApi, bool appApi, string qn = "0")
        {
            List<Video> videoTracks = new List<Video>();
            List<Audio> audioTracks = new List<Audio>();
            List<string> clips = new List<string>();
            List<string> dfns = new List<string>();

            //调用解析
            string webJsonStr = GetPlayJson(onlyAvc, aidOri, aid, cid, epId, tvApi, intlApi, appApi);

            JsonDocument respJson = JsonDocument.Parse(webJsonStr);

            //intl接口
            if (webJsonStr.Contains("\"stream_list\""))
            {
                int pDur = respJson.RootElement.GetProperty("data").GetProperty("video_info").GetProperty("timelength").GetInt32() / 1000;
                var audio = respJson.RootElement.GetProperty("data").GetProperty("video_info").GetProperty("dash_audio");//JArray.Parse(respJson.SelectToken("data.video_info.dash_audio").ToString());
                foreach(var stream in respJson.RootElement.GetProperty("data").GetProperty("video_info").GetProperty("stream_list").EnumerateArray())
                {
                    if (stream.TryGetProperty("dash_video",out var Video))
                    {
                        if (Video.GetProperty("base_url").GetString() != "")
                        {
                            Video v = new Video();
                            v.dur = pDur;
                            v.id = stream.GetProperty("stream_info").GetProperty("quality").ToString();
                            v.dfn = Program.qualitys[v.id];
                            v.bandwith = stream.GetProperty("dash_video").GetProperty("bandwidth").GetInt64() / 1000;
                            v.baseUrl = stream.GetProperty("dash_video").GetProperty("base_url").GetString();
                            v.codecs = stream.GetProperty("dash_video").GetProperty("codecid").ToString() == "12" ? "HEVC" : "AVC";
                            if (onlyHevc && v.codecs == "AVC") continue;
                            if (!videoTracks.Contains(v)) videoTracks.Add(v);
                        }
                    }
                }

                foreach(var node in audio.EnumerateArray())
                {
                    Audio a = new Audio();
                    a.id = node.GetProperty("id").ToString();
                    a.dfn = node.GetProperty("id").ToString();
                    a.dur = pDur;
                    a.bandwith = node.GetProperty("bandwidth").GetInt64() / 1000;
                    a.baseUrl = node.GetProperty("base_url").GetString();
                    a.codecs = "M4A";
                    audioTracks.Add(a);
                }

                return (webJsonStr, videoTracks, audioTracks, clips, dfns);
            }

            if (webJsonStr.Contains("\"dash\":{")) //dash
            {
                JsonElement audio = new JsonElement();
                JsonElement video = new JsonElement();
                int pDur = 0;
                string nodeName = "data";
                if (webJsonStr.Contains("\"result\":{"))
                {
                    nodeName = "result";
                }

                try { pDur = !tvApi ? respJson.RootElement.GetProperty(nodeName).GetProperty("dash").GetProperty("duration").GetInt32() : respJson.RootElement.GetProperty("dash").GetProperty("duration").GetInt32(); } catch { }
                try { pDur = !tvApi ? respJson.RootElement.GetProperty(nodeName).GetProperty("timelength").GetInt32() / 1000 : respJson.RootElement.GetProperty("timelength").GetInt32() / 1000; } catch { }

                bool reParse = false;
            reParse:
                if (reParse)
                {
                    webJsonStr = GetPlayJson(onlyAvc, aidOri, aid, cid, epId, tvApi, intlApi, appApi, GetMaxQn());
                    respJson = JsonDocument.Parse(webJsonStr);
                }
                try { video = !tvApi ? respJson.RootElement.GetProperty(nodeName).GetProperty("dash").GetProperty("video") : respJson.RootElement.GetProperty("dash").GetProperty("video"); } catch { }
                try { audio = !tvApi ? respJson.RootElement.GetProperty(nodeName).GetProperty("dash").GetProperty("audio") : respJson.RootElement.GetProperty("dash").GetProperty("audio"); } catch { }
                if (video.ValueKind == JsonValueKind.Array)
                {
                    foreach (var node in video.EnumerateArray())
                    {
                        Video v = new Video();
                        v.dur = pDur;
                        v.id = node.GetProperty("id").ToString();
                        v.dfn = Program.qualitys[node.GetProperty("id").ToString()];
                        v.bandwith = Convert.ToInt64(node.GetProperty("bandwidth").ToString()) / 1000;
                        v.baseUrl = node.GetProperty("base_url").GetString();
                        v.codecs = node.GetProperty("codecid").ToString() == "12" ? "HEVC" : "AVC";
                        if (!tvApi && !appApi)
                        {
                            v.res = node.GetProperty("width").ToString() + "x" + node.GetProperty("height").ToString();
                            v.fps = node.GetProperty("frame_rate").ToString();
                        }
                        if (onlyHevc && v.codecs == "AVC") continue;
                        if (onlyAvc && v.codecs == "HEVC") continue;
                        if (!videoTracks.Contains(v)) videoTracks.Add(v);
                    }
                }

                //此处处理免二压视频，需要单独再请求一次
                if (!reParse && !appApi)
                {
                    reParse = true;
                    goto reParse;
                }

                if (audio.ValueKind == JsonValueKind.Array)
                {
                    foreach (var node in audio.EnumerateArray())
                    {
                        Audio a = new Audio();
                        a.id = node.GetProperty("id").ToString();
                        a.dfn = node.GetProperty("id").ToString();
                        a.dur = pDur;
                        a.bandwith = Convert.ToInt64(node.GetProperty("bandwidth").ToString()) / 1000;
                        a.baseUrl = node.GetProperty("base_url").GetString();
                        a.codecs = node.GetProperty("codecs").GetString().Replace("mp4a.40.2", "M4A");
                        audioTracks.Add(a);
                    }
                }
            }
            else if (webJsonStr.Contains("\"durl\":[")) //flv
            {
                //默认以最高清晰度解析
                webJsonStr = GetPlayJson(onlyAvc, aidOri, aid, cid, epId, tvApi, intlApi, appApi, GetMaxQn());
                respJson = JsonDocument.Parse(webJsonStr);
                string quality = "";
                string videoCodecid = "";
                string url = "";
                double size = 0;
                double length = 0;
                if (webJsonStr.Contains("\"data\":{"))
                {
                    quality = respJson.RootElement.GetProperty("data").GetProperty("quality").ToString();
                    videoCodecid = respJson.RootElement.GetProperty("data").GetProperty("video_codecid").ToString();
                    //获取所有分段
                    foreach (var node in respJson.RootElement.GetProperty("data").GetProperty("durl").EnumerateArray())
                    {
                        clips.Add(node.GetProperty("url").GetString());
                        size += node.GetProperty("size").GetDouble();
                        length += node.GetProperty("length").GetDouble();
                    }
                    //TV模式可用清晰度
                    if (respJson.RootElement.GetProperty("data").GetProperty("qn_extras").ValueKind != JsonValueKind.Null)
                    {
                        foreach (var node in respJson.RootElement.GetProperty("data").GetProperty("qn_extras").EnumerateArray())
                        {
                            dfns.Add(node.GetProperty("qn").GetString());
                        }
                    }
                    else if (respJson.RootElement.GetProperty("data").GetProperty("accept_quality").ValueKind!= JsonValueKind.Null) //非tv模式可用清晰度
                    {
                        foreach (var node in respJson.RootElement.GetProperty("data").GetProperty("accept_quality").EnumerateArray())
                        {
                            string _qn = node.ToString();
                            if (_qn != null && _qn.Length > 0)
                                dfns.Add(node.ToString());
                        }
                    }
                }
                else
                {
                    //如果获取数据失败，尝试从根路径获取数据
                    quality = respJson.RootElement.GetProperty("quality").ToString();
                    videoCodecid = respJson.RootElement.GetProperty("video_codecid").ToString();
                    //获取所有分段
                    foreach (var node in respJson.RootElement.GetProperty("durl").EnumerateArray())
                    {
                        clips.Add(node.GetProperty("url").GetString());
                        size += node.GetProperty("size").GetDouble();
                        length += node.GetProperty("length").GetDouble();
                    }
                    //TV模式可用清晰度
                    if (respJson.RootElement.GetProperty("qn_extras").ValueKind != JsonValueKind.Null)
                    {
                        //获取可用清晰度
                        foreach (var node in respJson.RootElement.GetProperty("qn_extras").EnumerateArray())
                        {
                            dfns.Add(node.GetProperty("qn").ToString());
                        }
                    }                   
                    else if (respJson.RootElement.GetProperty("accept_quality").ValueKind != JsonValueKind.Null) //非tv模式可用清晰度
                    {
                        foreach (var node in respJson.RootElement.GetProperty("accept_quality").EnumerateArray())
                        {
                            string _qn = node.ToString();
                            if (_qn != null && _qn.Length > 0)
                                dfns.Add(node.ToString());
                        }
                    }
                }

                Video v = new Video();
                v.id = quality;
                v.dfn = Program.qualitys[quality];
                v.baseUrl = url;
                v.codecs = videoCodecid == "12" ? "HEVC" : "AVC";
                v.dur = (int)length / 1000;
                v.size = size;
                if (onlyHevc && v.codecs == "AVC") { }
                if (!videoTracks.Contains(v)) videoTracks.Add(v);
            }

            return (webJsonStr, videoTracks, audioTracks, clips, dfns);
        }
    }
}
