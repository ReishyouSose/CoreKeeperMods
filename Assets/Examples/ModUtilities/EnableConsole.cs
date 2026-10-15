using PugMod;
using UnityEngine;

public class EnableConsole : IMod
{
    public void EarlyInit()
    {
        // This enables Console Commands.
        Manager.enableConsole = true;
    }

    public void Init()
    {
    }

    public void ModObjectLoaded(Object obj)
    {
    }

    public void Shutdown()
    {
    }

    public void Update()
    {
    }
}
