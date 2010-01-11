﻿using System;
using System.Text;
using System.IO;
using System.Collections.Generic;

namespace MongoDB.Driver.GridFS
{
    public class GridFile{
        
        private const int DEFAULT_CHUNKSIZE = 256 * 1024;
        private const string DEFAULT_CONTENT_TYPE = "text/plain";
        
        private Database db;
        
        private string name;
        public string Name {
            get { return name; }
        }
        
        private IMongoCollection files;
        public IMongoCollection Files{
            get { return this.files; }
        }

        private IMongoCollection chunks;
        public IMongoCollection Chunks{
            get { return this.chunks; }
        }        
        
        public GridFile(Database db):this(db,"fs"){}

        public GridFile(Database db, string bucket){
            this.db = db;
            this.files = db[bucket + ".files"];
            this.chunks = db[bucket + ".chunks"];
            this.name = bucket;
        }
        
        public ICursor ListFiles(){
            return this.ListFiles(new Document());
        }
        
        public ICursor ListFiles(Document query){
            return this.files.Find(new Document().Append("query",query).Append("orderby", new Document().Append("filename", 1)));
        }
        
        public void Copy(String src, String dest){
            CodeWScope cw = new CodeWScope();
            String template ="function(){{\n" +
                            //"   print(\"Copying {1}\");\n" +
                            "   var srcdoc = db.{0}.files.findOne({{filename:\"{1}\"}});\n" +
                            "   if(srcdoc != undefined){{\n" +
                            "       var srcid = srcdoc._id;\n" +
                            "       var newid = ObjectId();\n" +
                            "       srcdoc._id = newid\n" +
                            "       srcdoc.filename = \"{2}\";\n" +
                            "       db.{0}.files.insert(srcdoc);\n" +
                            "       db.{0}.chunks.find({{files_id:srcid}}).forEach(function(chunk){{\n" +
                            //"           print(\"copying chunk...\");\n" +
                            "           chunk._id = ObjectId();\n" +
                            "           chunk.files_id = newid;\n" +
                            "           db.{0}.chunks.insert(chunk);\n" +
                            "       }});\n" +
                            "   }}" +
                            "}}";
            try{
                db.Eval(String.Format(template,this.name, src, dest));
            }catch(MongoCommandException mce){
                Console.WriteLine(mce.ToString());
            }
        }
        
        #region Create
        public GridFileStream Create(String filename){
            return Create(filename, FileMode.Create);
        }
        
        public GridFileStream Create(String filename, FileMode mode){
            return Create(filename,mode,FileAccess.ReadWrite);
        }
        
        public GridFileStream Create(String filename, FileMode mode, FileAccess access){
            //Create is delegated to a GridFileInfo because the stream needs access to the gfi and it
            //is easier to do it this way and only write the implementation once.
            GridFileInfo gfi = new GridFileInfo(this.db,this.name,filename);
            return gfi.Create(mode,access);

        }
        #endregion
        
        #region Delete
        public void Delete(Object id){
            files.Delete(new Document().Append("_id",id));
            chunks.Delete(new Document().Append("files_id",id));
        }
        
        public void Delete(String filename){
            files.Delete(new Document().Append("filename",filename));
        }
        
        public void Delete(Document query ){
            foreach(Document doc in ListFiles(query).Documents){
                Delete((Oid)doc["_id"]);
            }
        }
        #endregion
        
        #region Exists        
        public Boolean Exists(string name){
            return this.files.FindOne(new Document().Append("filename",name)) != null;
        }

        public Boolean Exists(Object id){
            return this.files.FindOne(new Document().Append("_id",id)) != null;
        }
        #endregion        
        
        #region Move
        public void Move(String src, String dest){
            this.files.Update(new Document().Append("$set", new Document().Append("filename",dest)), new Document().Append("filename", src));
        }
        
        public void Move(Object id, String dest){
            this.files.Update(new Document().Append("$set", new Document().Append("filename",dest)), new Document().Append("_id", id));
        }
        #endregion


        private void FlushWriteBuffer(byte[] buffer, )
        {
            List<Document> chunks = new List<Document>();
            int chunkNumber = 0;
            int offset = 0;
            int lastSize = (int)buffer.Length % chunkSize;
            double nthChunk = 0;
            if (buffer.Length > chunkSize)
            {
                nthChunk = Math.Floor(buffer.Length / (double)chunkSize);
                while (offset < buffer.Length)
                {
                    byte[] data = new byte[chunkSize];
                    if (chunkNumber < nthChunk){
                        Array.Copy(buffer, offset, data, 0, chunkSize);
                    }
                    else
                    {
                        Array.Copy(buffer, offset, data, 0, lastSize);
                    }
                    GridChunk gridChunk = new GridChunk(, chunkNumber, data);
                    chunks.Add(gridChunk.ToDocument());
                    offset += this.gridFileInfo.ChunkSize;
                    chunkNumber++;
                }
            }
            else
            {
                GridChunk gridChunk = new GridChunk(this.gridFileInfo.Id, 0, buffer);
                chunks.Add(gridChunk.ToDocument());
            }
        }
        
    }

}
