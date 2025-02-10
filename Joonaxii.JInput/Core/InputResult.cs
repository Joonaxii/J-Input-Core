namespace Joonaxii.JInput
{
    public struct InputResult
    {
        public InputCode code;
        public DeviceIndex device;
        public ulong tick;

        public InputResult(InputCode code, DeviceIndex device, ulong tick)
        {
            this.code = code;
            this.device = device;
            this.tick = tick;
        }
    }
}