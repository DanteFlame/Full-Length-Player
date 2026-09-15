using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
// Build-time, alpha-aware Lanczos-3 resampling. Each size is made from the original.
public static class IconRaster
{
    public static Rectangle Bounds(Bitmap image)
    {
        int left=image.Width, top=image.Height, right=-1, bottom=-1;
        bool transparent=false;
        for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) {
            byte a=image.GetPixel(x,y).A;
            if(a==0) transparent=true;
            if(a>0) { left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y); }
        }
        if(!transparent || right<left) throw new InvalidOperationException("Icon must contain artwork and genuine transparent pixels.");
        return Rectangle.FromLTRB(left,top,right+1,bottom+1);
    }
    struct Tap { public int Index; public double Weight; public Tap(int i,double w){Index=i;Weight=w;} }
    static double Kernel(double x) { x=Math.Abs(x); if(x<1e-9)return 1; if(x>=3)return 0; return Math.Sin(Math.PI*x)*Math.Sin(Math.PI*x/3)/(Math.PI*Math.PI*x*x/3); }
    static Tap[][] Weights(int source,int target) {
        double scale=Math.Max(1,source/(double)target);
        var result=new Tap[target][];
        for(int p=0;p<target;p++) {
            double center=(p+.5)*source/target-.5, sum=0;
            var taps=new List<Tap>();
            for(int q=(int)Math.Ceiling(center-3*scale);q<=(int)Math.Floor(center+3*scale);q++) {
                double weight=Kernel((q-center)/scale);sum+=weight;
                taps.Add(new Tap(Math.Max(0,Math.Min(source-1,q)),weight));
            }
            for(int q=0;q<taps.Count;q++) taps[q]=new Tap(taps[q].Index,taps[q].Weight/sum);
            result[p]=taps.ToArray();
        }
        return result;
    }
    static double Clamp(double v) { return Math.Max(0,Math.Min(1,v)); }
    public static Bitmap Render(Bitmap image,Rectangle crop,int size) {
        int pad=Math.Max(1,(int)Math.Round(size*.015));
        double factor=(size-2*pad)/(double)Math.Max(crop.Width,crop.Height);
        int w=Math.Max(1,(int)Math.Round(crop.Width*factor)), h=Math.Max(1,(int)Math.Round(crop.Height*factor));
        var original=new float[crop.Width*crop.Height*4];
        for(int y=0;y<crop.Height;y++) for(int x=0;x<crop.Width;x++) {
            var c=image.GetPixel(crop.X+x,crop.Y+y); int i=(y*crop.Width+x)*4;float a=c.A/255f;
            original[i]=c.R/255f*a;original[i+1]=c.G/255f*a;original[i+2]=c.B/255f*a;original[i+3]=a;
        }
        var wx=Weights(crop.Width,w);var wy=Weights(crop.Height,h);
        var horizontal=new float[w*crop.Height*4];
        for(int y=0;y<crop.Height;y++) for(int x=0;x<w;x++) for(int k=0;k<4;k++) {
            double sum=0;foreach(var t in wx[x])sum+=original[(y*crop.Width+t.Index)*4+k]*t.Weight;
            horizontal[(y*w+x)*4+k]=(float)sum;
        }
        var pixels=new float[w*h*4];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)for(int k=0;k<4;k++) {
            double sum=0;foreach(var t in wy[y])sum+=horizontal[(t.Index*w+x)*4+k]*t.Weight;
            pixels[(y*w+x)*4+k]=(float)sum;
        }
        var result=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        int ox=(size-w)/2,oy=(size-h)/2;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
            int i=(y*w+x)*4; double a=Clamp(pixels[i+3]);
            if(a<.5/255)continue;
            var rgb=new int[3];
            for(int k=0;k<3;k++) {
                double value=Clamp(pixels[i+k]/a);
                if(size<=64 && a>.25) {
                    double blur=0,ba=0;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++) {
                        int j=(Math.Max(0,Math.Min(h-1,y+dy))*w+Math.Max(0,Math.Min(w-1,x+dx)))*4;
                        int weight=(dx==0?2:1)*(dy==0?2:1);
                        blur+=pixels[j+k]*weight;ba+=pixels[j+3]*weight;
                    }
                    if(ba>0) { double delta=value-blur/ba; if(Math.Abs(delta)>1.0/255)value=Clamp(value+.22*delta); }
                }
                rgb[k]=(int)Math.Round(255*value);
            }
            result.SetPixel(ox+x,oy+y,Color.FromArgb((int)Math.Round(a*255),rgb[0],rgb[1],rgb[2]));
        }
        return result;
    }
}
