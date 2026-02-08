#nullable disable
namespace HC.BnnSoftSigns
{
    public class SignPdfInput
    {
        public string fontcolor { get; set; }
        public string fontname { get; set; }
        public int fontstyle { get; set; }
        public int fontsize { get; set; }
        public byte[] anhkhung { get; set; }
        public byte[] condau { get; set; }
        public byte[] chukytuoi { get; set; }
        public byte[] datapdf { get; set; }
        public string hashalg { get; set; }
        public string textsign { get; set; }
        public int height { get; set; }
        public int pagesign { get; set; }
        public string signaturename { get; set; }
        public string textout { get; set; }
        public int typesignature { get; set; }//2: sign with text, 1: sign with image , 3 ca 2 
        public int width { get; set; }
        public int xpoint { get; set; }
        public int ypoint { get; set; }
        public int imgabsolutepositionx { get; set; }
        public int imgabsolutepositiony { get; set; }
        public int imgwidth { get; set; }
        public int imgheight { get; set; }
        public int borderstyle { get; set; }
        public string bordercolor { get; set; }
        public int padleft { get; set; }
        public int padtop { get; set; }
        public float textscale { get; set; }
        public float tylecondau { get; set; }
        public float tylechukytuoi { get; set; }
        public string nguoiky { get; set; }
        public string chucvu { get; set; }
    }
}
