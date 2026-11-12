// Copyright (c) 2026 EnusDE. MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
namespace EnusDE.Communications
{
    public sealed class HostArchive
    {
        readonly string directory;
        readonly Func<DateTime> utc;
        readonly SortedDictionary<long,List<string>> files=new SortedDictionary<long,List<string>>();
        public const int RetentionHours=6;
        public HostArchive(string path,Func<DateTime> clock=null)
        {
            directory=Path.GetFullPath(path);utc=clock??(()=>DateTime.UtcNow);Directory.CreateDirectory(directory);
            foreach(string file in Directory.GetFiles(directory,"*.comm.json*")){long ticks;var name=Path.GetFileName(file);if(name.Length>=19&&Int64.TryParse(name.Substring(0,19),out ticks))Track(ticks,file);}Purge();
        }
        void Track(long ticks,string path){List<string> rows;if(!files.TryGetValue(ticks,out rows))files[ticks]=rows=new List<string>();rows.Add(path);}
        public void Append(ChatRecord record)
        {
            if(record==null||record.Utc.Kind!=DateTimeKind.Utc)throw new InvalidDataException("UTC record required.");
            var name=record.Utc.Ticks.ToString("D19")+"-"+Guid.NewGuid().ToString("N")+".comm.json";
            string path=Path.Combine(directory,name),temp=path+".tmp";
            byte[] bytes=new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(record,Formatting.None));
            try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}File.Move(temp,path);Track(record.Utc.Ticks,path);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public void Purge()
        {
            long cutoff=utc().AddHours(-RetentionHours).Ticks;
            while(files.Count>0){var first=files.First();if(first.Key>cutoff)break;foreach(string file in first.Value)File.Delete(file);files.Remove(first.Key);}
        }
        public List<ChatRecord> Read()
        {
            Purge();var rows=new List<ChatRecord>();
            foreach(string path in Directory.GetFiles(directory,"*.comm.json"))
            {try{var r=JsonConvert.DeserializeObject<ChatRecord>(File.ReadAllText(path));if(r!=null&&r.Utc>utc().AddHours(-RetentionHours))rows.Add(r);}catch(JsonException){}}
            return rows.OrderByDescending(r=>r.Utc).ToList();
        }
    }
}
