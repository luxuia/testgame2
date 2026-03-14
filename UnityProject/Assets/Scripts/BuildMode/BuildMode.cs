namespace Minecraft.BuildMode
{
    public enum BuildMode
    {
        /// <summary> Place blocks in selected area. </summary>
        Construct,

        /// <summary> Remove blocks (deconstruct) in selected area. </summary>
        Deconstruct,

        /// <summary> Mine/harvest blocks in selected area (same as deconstruct for now, may drop items). </summary>
        Mine
    }
}
