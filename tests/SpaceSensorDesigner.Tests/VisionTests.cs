using SpaceSensorDesigner.Core.Vision;
using Xunit;

namespace SpaceSensorDesigner.Tests;

public class VisionTests
{
    private static byte[] WhiteCanvas(int w, int h)
    {
        var g = new byte[w * h];
        for (int i = 0; i < g.Length; i++) g[i] = 255;
        return g;
    }

    [Fact]
    public void Detect_FindsTwoRoomsSeparatedByAPartition()
    {
        int w = 120, h = 80;
        var g = WhiteCanvas(w, h);
        void H(int x0, int x1, int y) { for (int x = x0; x <= x1; x++) g[y * w + x] = 0; }
        void V(int y0, int y1, int x) { for (int y = y0; y <= y1; y++) g[y * w + x] = 0; }

        // Outer rectangle of walls + one vertical partition → two rooms.
        H(10, 110, 10); H(10, 110, 70); V(10, 70, 10); V(10, 70, 110);
        V(10, 70, 60);

        var rects = RoomDetector.Detect(g, w, h);

        Assert.Equal(2, rects.Count);
        // Both rooms live inside the outer walls.
        Assert.All(rects, r => Assert.True(r.MinX >= 10 && r.MaxX <= 110 && r.MinY >= 10 && r.MaxY <= 70));
        // They sit on opposite sides of the x=60 partition.
        Assert.Contains(rects, r => r.MaxX < 60);
        Assert.Contains(rects, r => r.MinX > 60);
    }

    [Fact]
    public void Detect_SealsDoorGap_SoAdjacentRoomsSeparate()
    {
        int w = 160, h = 90;
        var g = WhiteCanvas(w, h);
        void H(int x0, int x1, int y) { for (int x = x0; x <= x1; x++) g[y * w + x] = 0; }
        void V(int y0, int y1, int x) { for (int y = y0; y <= y1; y++) g[y * w + x] = 0; }

        H(10, 150, 10); H(10, 150, 80); V(10, 80, 10); V(10, 80, 150);
        // Partition at x=80 with an ~11 px door opening (y 40..50 left open).
        V(10, 39, 80); V(51, 80, 80);

        // No sealing → the rooms leak into one region through the doorway.
        var noSeal = RoomDetector.Detect(g, w, h, new RoomDetectionOptions { WallSealRadius = 0 });
        Assert.Single(noSeal);

        // Sealing radius past half the gap closes the doorway → two separate rooms.
        var sealed2 = RoomDetector.Detect(g, w, h, new RoomDetectionOptions { WallSealRadius = 8 });
        Assert.Equal(2, sealed2.Count);
    }

    [Fact]
    public void Detect_BlankImage_FindsNoRooms()
    {
        var rects = RoomDetector.Detect(WhiteCanvas(100, 100), 100, 100);
        Assert.Empty(rects); // one big region that touches the border = exterior, not a room
    }
}
