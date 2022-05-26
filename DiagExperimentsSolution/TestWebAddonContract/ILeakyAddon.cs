namespace TestWebAddonContract
{
    /// <summary>
    /// An interface used to call the LeayAddon class from a different Load Context
    /// </summary>
    public interface ILeakyAddon
    {
        void LeakSomeMemory(int num);
        byte[] AllocateSomeMemory(int size);
    }
}