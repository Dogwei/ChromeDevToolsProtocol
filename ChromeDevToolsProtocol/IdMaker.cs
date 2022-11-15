namespace ChromeDevToolsProtocol
{
    sealed class IdMaker
    {
        private int internal_id;

        public int MakeId()
        {
            return Interlocked.Increment(ref internal_id);
        }
    }
}