namespace Joonaxii.JInput
{
    public struct MouseData
    {
        public float xPos;
        public float yPos;

        public float xScroll;
        public float yScroll;

        public MouseData(float xPos, float yPos, float xScroll, float yScroll)
        {
            this.xPos = xPos;
            this.yPos = yPos;
            this.xScroll = xScroll;
            this.yScroll = yScroll;
        }

        public static MouseData operator +(MouseData a, MouseData b) =>
            new MouseData(a.xPos + b.xPos, a.yPos + b.yPos, a.xScroll + b.xScroll, a.yScroll + b.yScroll);

        public static MouseData operator -(MouseData a, MouseData b) => 
            new MouseData(a.xPos - b.xPos, a.yPos - b.yPos, a.xScroll - b.xScroll, a.yScroll - b.yScroll);
    }
}