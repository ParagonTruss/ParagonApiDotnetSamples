using ParagonApi;
using ParagonApi.Models;

namespace LayoutVisualizationConsoleApp;

public static class Program
{
    /// <summary>
    /// Replace with a known project GUID
    /// </summary>
    private static readonly Guid ProjectGuid = Guid.Parse("81cda3cc-dbf3-466e-8bf7-9f89e6776b63");

    public static async Task Main()
    {
        Console.WriteLine("Connecting to the Paragon API...");
        using var connection = await Paragon.Connect();

        Console.WriteLine("Fetching project...");
        var project = await connection.Projects.Get(ProjectGuid);

        Console.WriteLine(
            "Fetching component designs and logging truss geometry in Design coordinates..."
        );
        Console.WriteLine();

        foreach (var componentDesignGuid in project.ComponentDesignGuids)
        {
            Console.WriteLine($"Fetching {componentDesignGuid}...");
            var truss = await connection.Trusses.Find(componentDesignGuid);

            Console.WriteLine($"Member geometries for Truss {truss.Name} in Design space:");
            foreach (var member in truss.Members)
            {
                var memberCoordinates = string.Join(
                    ", ",
                    member.Geometry.Select(vertex => $"({vertex.X}, {vertex.Y})")
                );
                Console.WriteLine($"{member.Name}: {memberCoordinates}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("Fetching truss envelopes...");
        var trussEnvelopes = await connection.Layouts.GetTrussEnvelopes(ProjectGuid);

        Console.WriteLine(
            "Fetching component designs and logging truss geometry in Layout coordinates..."
        );
        Console.WriteLine();

        foreach (var trussEnvelope in trussEnvelopes)
        {
            if (!trussEnvelope.ComponentDesignGuid.HasValue)
                continue;

            var componentDesignGuid = trussEnvelope.ComponentDesignGuid.Value;

            Console.WriteLine($"Fetching {componentDesignGuid}...");
            var truss = await connection.Trusses.Find(componentDesignGuid);

            Console.WriteLine(
                $"Member geometries for Truss Envelope {trussEnvelope.Name} in Layout space:"
            );
            foreach (var member in truss.Members)
            {
                var memberGeometryInLayoutSpace = GetMember3DVerticesInLayoutSpace(
                    member,
                    trussEnvelope.LeftPoint,
                    trussEnvelope.RightPoint,
                    trussEnvelope.Elevation
                );

                var memberCoordinates = string.Join(
                    ", ",
                    memberGeometryInLayoutSpace.Select(vertex =>
                        $"({vertex.X}, {vertex.Y}, {vertex.Z})"
                    )
                );
                Console.WriteLine($"{member.Name}: {memberCoordinates}");
            }

            Console.WriteLine();
        }
    }

    private static List<Point3D> GetMember3DVerticesInLayoutSpace(
        Member member,
        Point2D leftPoint,
        Point2D rightPoint,
        double elevation
    )
    {
        var extrudedMemberGeometry = ExtrudeInPositiveZDirection(member.Geometry, member.Thickness);
        var extrudedMemberGeometryInLayoutCoordinates = extrudedMemberGeometry
            .Select(ConvertFromDesignToLayoutCoordinates)
            .ToList();

        var rotation = Math.Atan2(rightPoint.Y - leftPoint.Y, rightPoint.X - leftPoint.X);
        var rotationMatrix = RotationMatrix.AroundZAxis(rotation);

        var translation = new Point3D
        {
            X = leftPoint.X,
            Y = leftPoint.Y,
            Z = elevation,
        };

        return extrudedMemberGeometryInLayoutCoordinates
            .Select(vertex =>
            {
                var rotatedVertex = rotationMatrix.ApplyToPoint(vertex);
                return TranslatePoint(rotatedVertex, translation);
            })
            .ToList();
    }

    private static Point3D ConvertFromDesignToLayoutCoordinates(Point3D point)
    {
        return new Point3D
        {
            X = point.X,
            Y = -point.Z,
            Z = point.Y,
        };
    }

    private static List<Point3D> ExtrudeInPositiveZDirection(
        IReadOnlyList<Point2D> vertices,
        double extrusion
    )
    {
        return vertices
            .Select(vertex => new Point3D
            {
                X = vertex.X,
                Y = vertex.Y,
                Z = 0,
            })
            .Concat(
                vertices.Select(vertex => new Point3D
                {
                    X = vertex.X,
                    Y = vertex.Y,
                    Z = extrusion,
                })
            )
            .ToList();
    }

    private static Point3D TranslatePoint(Point3D point, Point3D translation)
    {
        return new Point3D
        {
            X = point.X + translation.X,
            Y = point.Y + translation.Y,
            Z = point.Z + translation.Z,
        };
    }
}

public class RotationMatrix
{
    /// <summary>
    /// 3x3 matrix in column-major order
    /// </summary>
    private readonly double[] _matrix;

    private RotationMatrix(double[] matrix)
    {
        _matrix = matrix;
    }

    /// <summary>
    /// Creates a matrix that performs a rotation around the Z-axis.
    /// </summary>
    public static RotationMatrix AroundZAxis(double rotation)
    {
        var matrix = new double[16];

        matrix[0] = Math.Cos(rotation);
        matrix[1] = Math.Sin(rotation);
        matrix[2] = 0;

        matrix[3] = -Math.Sin(rotation);
        matrix[4] = Math.Cos(rotation);
        matrix[5] = 0;

        matrix[6] = 0;
        matrix[7] = 0;
        matrix[8] = 1;

        return new RotationMatrix(matrix);
    }

    public Point3D ApplyToPoint(Point3D point)
    {
        var x = point.X;
        var y = point.Y;
        var z = point.Z;
        var m = _matrix;

        return new Point3D
        {
            X = x * m[0] + y * m[3] + z * m[6],
            Y = x * m[1] + y * m[4] + z * m[7],
            Z = x * m[2] + y * m[5] + z * m[8],
        };
    }
}
