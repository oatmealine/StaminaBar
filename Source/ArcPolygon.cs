using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.StaminaBar;

public class ArcPolygon {
    public int Resolution;

    public float AngleStart = 0f;
    public float AngleEnd = 1f;
    public Color Color = Color.White;

    public float OuterRadius = 1f;
    public float InnerRadius = .25f;
    
    private VertexPositionColor[] vertices;
    private int[] indices;
    
    public ArcPolygon(int resolution) {
        SetResolution(resolution);
    }

    public void SetResolution(int resolution) {
        Resolution = resolution;
        
        vertices = new VertexPositionColor[Resolution * 2];
        indices = new int[Resolution * 2 * 3];
        
        Rebuild();
    }

    public void Rebuild() {
        BuildVertices();
        BuildIndices();
    }

    // stupid optimizations for stupid babies
    public bool IsClosed() {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        //return (AngleStart % 1f) == (AngleEnd % 1f);
        return false;
    }
    
    private void BuildVertices() {
        var holeRatio = InnerRadius / OuterRadius;
        for (var i = 0; i < Resolution; i++) {
            var alpha = (float)i / (IsClosed() ? Resolution : Resolution - 1);
            var aMin = Math.Min(AngleStart, AngleEnd);
            var aMax = Math.Max(AngleStart, AngleEnd);
            
            var angle = (aMin + alpha * (aMax - aMin)) * float.Tau;
            var cos = (float)Math.Sin(angle);
            var sin = (float)Math.Cos(angle);
            
            vertices[i*2  ] = new VertexPositionColor(new Vector3(cos,             -sin,             0f), Color);
            vertices[i*2+1] = new VertexPositionColor(new Vector3(cos * holeRatio, -sin * holeRatio, 0f), Color);
        }
    }
    
    private void BuildIndices() {
        for (var i = 0; i < (IsClosed() ? Resolution : Resolution - 1); i++) {
            indices[i * 6 + 0] = (i * 2    ) % (Resolution * 2);
            indices[i * 6 + 1] = (i * 2 + 1) % (Resolution * 2);
            indices[i * 6 + 2] = (i * 2 + 2) % (Resolution * 2);
            
            indices[i * 6 + 3] = (i * 2 + 1) % (Resolution * 2);
            indices[i * 6 + 4] = (i * 2 + 2) % (Resolution * 2);
            indices[i * 6 + 5] = (i * 2 + 3) % (Resolution * 2);
        }
    }

    public void Draw(Vector2 drawPos, float scale) {
        var m = Matrix.Identity;
        m *= Matrix.CreateScale(OuterRadius * scale, OuterRadius * scale, 1f);
        m *= Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0f);
        
        GFX.DrawIndexedVertices(
            m,
            vertices, vertices.Length,
            indices, indices.Length / 3
        );
    }
}