# Original geometric animation; no downloaded content, codec or encoder dependency.
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Text;
public static class MuralDemoVideo
{
    static void Four(BinaryWriter w, string id) { w.Write(Encoding.ASCII.GetBytes(id)); }
    static long Begin(BinaryWriter w, string id) { Four(w,id); long p=w.BaseStream.Position; w.Write(0); return p; }
    static void End(BinaryWriter w, long p) { long end=w.BaseStream.Position; w.BaseStream.Position=p; w.Write((int)(end-p-4)); w.BaseStream.Position=end; }
    public static void Create(string path)
    {
        const int width=160, height=90, fps=12, frames=36, size=width*height*3;
        using(var w=new BinaryWriter(File.Create(path)))
        {
            long riff=Begin(w,"RIFF"); Four(w,"AVI ");
            long hdrl=Begin(w,"LIST"); Four(w,"hdrl");
            long avih=Begin(w,"avih");
            w.Write(1000000/fps); w.Write(size*fps); w.Write(0); w.Write(0x10);
            w.Write(frames); w.Write(0); w.Write(1); w.Write(size); w.Write(width); w.Write(height);
            for(int i=0;i<4;i++)w.Write(0); End(w,avih);
            long strl=Begin(w,"LIST"); Four(w,"strl");
            long strh=Begin(w,"strh"); Four(w,"vids"); Four(w,"DIB ");
            w.Write(0); w.Write((short)0); w.Write((short)0); w.Write(0);
            w.Write(1); w.Write(fps); w.Write(0); w.Write(frames); w.Write(size); w.Write(-1); w.Write(0);
            w.Write((short)0); w.Write((short)0); w.Write((short)width); w.Write((short)height); End(w,strh);
            long strf=Begin(w,"strf"); w.Write(40); w.Write(width); w.Write(height);
            w.Write((short)1); w.Write((short)24); w.Write(0); w.Write(size);
            w.Write(0); w.Write(0); w.Write(0); w.Write(0); End(w,strf); End(w,strl); End(w,hdrl);
            long movi=Begin(w,"LIST"); long origin=w.BaseStream.Position; Four(w,"movi");
            int[] offsets=new int[frames]; byte[] pixels=new byte[size];
            for(int f=0;f<frames;f++)
            {
                double cx=80+28*Math.Sin(f*2*Math.PI/frames), cy=45+12*Math.Cos(f*2*Math.PI/frames);
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    bool circle=(x-cx)*(x-cx)+(y-cy)*(y-cy)<225;
                    bool stripe=y>=74 && x>=40 && x<40+(f+1)*80/frames;
                    int p=((height-1-y)*width+x)*3;
                    pixels[p]=(byte)(circle?210:stripe?190:90+x/8);
                    pixels[p+1]=(byte)(circle?225:stripe?200:60+y/4);
                    pixels[p+2]=(byte)(circle?245:stripe?220:25);
                }
                offsets[f]=(int)(w.BaseStream.Position-origin);
                long chunk=Begin(w,"00db"); w.Write(pixels); End(w,chunk);
            }
            End(w,movi); long idx=Begin(w,"idx1");
            for(int f=0;f<frames;f++){Four(w,"00db");w.Write(0x10);w.Write(offsets[f]);w.Write(size);}
            End(w,idx); End(w,riff);
        }
    }
}
'@
$videoAssetDirectory = Join-Path $PSScriptRoot '..\Assets'
New-Item -ItemType Directory -Path $videoAssetDirectory -Force | Out-Null
[MuralDemoVideo]::Create((Join-Path $videoAssetDirectory 'demo-mural.avi'))
Get-Item (Join-Path $videoAssetDirectory 'demo-mural.avi') | Select-Object Name,Length
