using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrabsBeachMain : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Clayxels.ClayContainer.setOutputRenderTextureSize(1024, 512);
        Clayxels.ClayContainer.setGlobalBlend(0.5f);
        Clayxels.ClayContainer.setUpdateFrameSkip(10);
        Clayxels.ClayContainer.initGlobalData();
        Clayxels.ClayContainer.forceAllContainersInit();
    }
}
