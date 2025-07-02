using ParagonApi;
using ParagonApi.Models;

namespace LayoutConsoleApp;

public static class Program
{
    private static double Feet => 12;

    private static double BuildingLength => 60 * Feet;
    private static double Span => 24 * Feet;
    private static double OverhangDistance => 2 * Feet;
    private static double WallTop => 8 * Feet;

    public static async Task Main()
    {
        Console.WriteLine("Connecting to the Paragon API...");
        using var connection = await Paragon.Connect();

        Console.WriteLine("Creating a new project...");
        var project = await connection.Projects.Create(new NewProject { Name = "Test Project" });

        Console.WriteLine("Creating bearing envelopes and roof planes...");
        await CreateBearingEnvelopesAndRoofPlanes(connection, project.Guid);

        Console.WriteLine("Creating truss envelopes...");
        var trussEnvelopes = await CreateTrussEnvelopes(connection, project.Guid);

        Console.WriteLine("Creating trusses...");
        trussEnvelopes = await connection.Layouts.CreateTrusses(
            project.Guid,
            trussEnvelopes.Select(trussEnvelope => trussEnvelope.Guid).ToList()
        );

        Console.WriteLine("Upgrading and analyzing trusses...");
        Console.WriteLine();

        var trussGuids = trussEnvelopes
            .Select(trussEnvelope => trussEnvelope.ComponentDesignGuid)
            .Where(guid => guid.HasValue)
            .Select(guid => guid!.Value)
            .Distinct();

        foreach (var trussGuid in trussGuids)
        {
            var truss = await connection.Trusses.Find(trussGuid);
            var analysisSet = await connection.Trusses.UpgradeAndAnalyze(trussGuid);

            Console.WriteLine($"Truss {truss.Name} Capacity Ratio: {analysisSet.CapacityRatio}");
        }

        Console.WriteLine();
        Console.WriteLine($"Project URL: https://design.paragontruss.com/{project.Guid}");
        Console.WriteLine();
    }

    private static async Task CreateBearingEnvelopesAndRoofPlanes(
        Paragon connection,
        Guid projectGuid
    )
    {
        var southWestPoint = new Point2D { X = 0, Y = 0 };
        var southEastPoint = new Point2D { X = BuildingLength, Y = 0 };
        var northEastPoint = new Point2D { X = BuildingLength, Y = Span };
        var northWestPoint = new Point2D { X = 0, Y = Span };

        var southBearingEnvelope = await connection.Layouts.CreateBearingEnvelope(
            projectGuid,
            CreateNewBearingEnvelope(
                name: "South",
                leftPoint: southWestPoint,
                rightPoint: southEastPoint
            )
        );
        var eastBearingEnvelope = await connection.Layouts.CreateBearingEnvelope(
            projectGuid,
            CreateNewBearingEnvelope(
                name: "East",
                leftPoint: southEastPoint,
                rightPoint: northEastPoint
            )
        );
        var northBearingEnvelope = await connection.Layouts.CreateBearingEnvelope(
            projectGuid,
            CreateNewBearingEnvelope(
                name: "North",
                leftPoint: northEastPoint,
                rightPoint: northWestPoint
            )
        );
        var westBearingEnvelope = await connection.Layouts.CreateBearingEnvelope(
            projectGuid,
            CreateNewBearingEnvelope(
                name: "West",
                leftPoint: northWestPoint,
                rightPoint: southWestPoint
            )
        );

        var southRoofPlane = await connection.Layouts.CreateRoofPlane(
            projectGuid,
            CreateNewRoofPlane(southBearingEnvelope.Guid)
        );
        var eastRoofPlane = await connection.Layouts.CreateRoofPlane(
            projectGuid,
            CreateNewRoofPlane(eastBearingEnvelope.Guid)
        );
        var northRoofPlane = await connection.Layouts.CreateRoofPlane(
            projectGuid,
            CreateNewRoofPlane(northBearingEnvelope.Guid)
        );
        var westRoofPlane = await connection.Layouts.CreateRoofPlane(
            projectGuid,
            CreateNewRoofPlane(westBearingEnvelope.Guid)
        );

        southRoofPlane.Cuts.AddRange(
            CreatePlaneCuts([westRoofPlane, northRoofPlane, eastRoofPlane])
        );
        await connection.Layouts.UpdateRoofPlane(projectGuid, southRoofPlane);

        westRoofPlane.Cuts.AddRange(CreatePlaneCuts([northRoofPlane, southRoofPlane]));
        await connection.Layouts.UpdateRoofPlane(projectGuid, westRoofPlane);

        northRoofPlane.Cuts.AddRange(
            CreatePlaneCuts([eastRoofPlane, southRoofPlane, westRoofPlane])
        );
        await connection.Layouts.UpdateRoofPlane(projectGuid, northRoofPlane);

        eastRoofPlane.Cuts.AddRange(CreatePlaneCuts([southRoofPlane, northRoofPlane]));
        await connection.Layouts.UpdateRoofPlane(projectGuid, eastRoofPlane);
    }

    private static async Task<List<TrussEnvelope>> CreateTrussEnvelopes(
        Paragon connection,
        Guid projectGuid
    )
    {
        var trussEnvelopes = new List<TrussEnvelope>();
        var trussEnvelopeNumber = 1;

        var girderOffset = 7 * Feet;
        var westGirder = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = girderOffset, Y = 0 },
                rightPoint: new Point2D { X = girderOffset, Y = Span },
                Justification.Back,
                leftOverhang: OverhangDistance,
                rightOverhang: OverhangDistance
            )
        );
        trussEnvelopes.Add(westGirder);

        var eastGirder = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = BuildingLength - girderOffset, Y = 0 },
                rightPoint: new Point2D { X = BuildingLength - girderOffset, Y = Span },
                Justification.Front,
                leftOverhang: OverhangDistance,
                rightOverhang: OverhangDistance
            )
        );
        trussEnvelopes.Add(eastGirder);

        for (
            var trussOffset = girderOffset + 2 * Feet;
            trussOffset < BuildingLength - girderOffset;
            trussOffset += 2 * Feet
        )
        {
            var common = await connection.Layouts.CreateTrussEnvelope(
                projectGuid,
                CreateNewTrussEnvelope(
                    name: $"{trussEnvelopeNumber++}",
                    leftPoint: new Point2D { X = trussOffset, Y = 0 },
                    rightPoint: new Point2D { X = trussOffset, Y = Span },
                    Justification.Back,
                    leftOverhang: OverhangDistance,
                    rightOverhang: OverhangDistance
                )
            );
            trussEnvelopes.Add(common);
        }

        var southwestEndJack = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = 0, Y = girderOffset },
                rightPoint: new Point2D { X = girderOffset, Y = girderOffset },
                Justification.Front,
                leftOverhang: OverhangDistance,
                rightOverhang: 0
            )
        );
        trussEnvelopes.Add(southwestEndJack);

        var northwestEndJack = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = 0, Y = Span - girderOffset },
                rightPoint: new Point2D { X = girderOffset, Y = Span - girderOffset },
                Justification.Back,
                leftOverhang: OverhangDistance,
                rightOverhang: 0
            )
        );
        trussEnvelopes.Add(northwestEndJack);

        for (
            var trussOffset = girderOffset + 2 * Feet;
            trussOffset <= Span - girderOffset - 2 * Feet;
            trussOffset += 2 * Feet
        )
        {
            var endJack = await connection.Layouts.CreateTrussEnvelope(
                projectGuid,
                CreateNewTrussEnvelope(
                    name: $"{trussEnvelopeNumber++}",
                    leftPoint: new Point2D { X = 0, Y = trussOffset },
                    rightPoint: new Point2D { X = girderOffset, Y = trussOffset },
                    Justification.Back,
                    leftOverhang: OverhangDistance,
                    rightOverhang: 0
                )
            );
            trussEnvelopes.Add(endJack);
        }

        var southeastEndJack = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = BuildingLength - girderOffset, Y = girderOffset },
                rightPoint: new Point2D { X = BuildingLength, Y = girderOffset },
                Justification.Front,
                leftOverhang: 0,
                rightOverhang: OverhangDistance
            )
        );
        trussEnvelopes.Add(southeastEndJack);

        var northeastEndJack = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D
                {
                    X = BuildingLength - girderOffset,
                    Y = Span - girderOffset,
                },
                rightPoint: new Point2D { X = BuildingLength, Y = Span - girderOffset },
                Justification.Back,
                leftOverhang: 0,
                rightOverhang: OverhangDistance
            )
        );
        trussEnvelopes.Add(northeastEndJack);

        for (
            var trussOffset = girderOffset + 2 * Feet;
            trussOffset <= Span - girderOffset - 2 * Feet;
            trussOffset += 2 * Feet
        )
        {
            var endJack = await connection.Layouts.CreateTrussEnvelope(
                projectGuid,
                CreateNewTrussEnvelope(
                    name: $"{trussEnvelopeNumber++}",
                    leftPoint: new Point2D { X = BuildingLength - girderOffset, Y = trussOffset },
                    rightPoint: new Point2D { X = BuildingLength, Y = trussOffset },
                    Justification.Back,
                    leftOverhang: 0,
                    rightOverhang: OverhangDistance
                )
            );
            trussEnvelopes.Add(endJack);
        }

        var kingJackOffset = 1.5 / Math.Sqrt(2) / 2;
        var kingJackOverhangDistance = OverhangDistance * Math.Sqrt(2);

        var southwestKingJack = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = kingJackOffset, Y = kingJackOffset },
                rightPoint: new Point2D { X = girderOffset, Y = girderOffset },
                Justification.Center,
                leftOverhang: kingJackOverhangDistance,
                rightOverhang: 0
            )
        );
        trussEnvelopes.Add(southwestKingJack);

        var northwestKingJack = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = kingJackOffset, Y = Span - kingJackOffset },
                rightPoint: new Point2D { X = girderOffset, Y = Span - girderOffset },
                Justification.Center,
                leftOverhang: kingJackOverhangDistance,
                rightOverhang: 0
            )
        );
        trussEnvelopes.Add(northwestKingJack);

        var southeastKingJack = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = BuildingLength - kingJackOffset, Y = kingJackOffset },
                rightPoint: new Point2D { X = BuildingLength - girderOffset, Y = girderOffset },
                Justification.Center,
                leftOverhang: kingJackOverhangDistance,
                rightOverhang: 0
            )
        );
        trussEnvelopes.Add(southeastKingJack);

        var northeastKingJack = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D
                {
                    X = BuildingLength - kingJackOffset,
                    Y = Span - kingJackOffset,
                },
                rightPoint: new Point2D
                {
                    X = BuildingLength - girderOffset,
                    Y = Span - girderOffset,
                },
                Justification.Center,
                leftOverhang: kingJackOverhangDistance,
                rightOverhang: 0
            )
        );
        trussEnvelopes.Add(northeastKingJack);

        var cornerJackOffset = 0.75 * Math.Sqrt(2) - 0.75;

        var southwestHorizontalCornerJack1 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = 0, Y = girderOffset - 2 * Feet },
                rightPoint: new Point2D
                {
                    X = girderOffset - 2 * Feet - cornerJackOffset,
                    Y = girderOffset - 2 * Feet,
                },
                Justification.Front,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(southwestHorizontalCornerJack1);

        var southwestHorizontalCornerJack2 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = 0, Y = girderOffset - 4 * Feet },
                rightPoint: new Point2D
                {
                    X = girderOffset - 4 * Feet - cornerJackOffset,
                    Y = girderOffset - 4 * Feet,
                },
                Justification.Front,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(southwestHorizontalCornerJack2);

        var southwestVerticalCornerJack1 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = girderOffset - 2 * Feet, Y = 0 },
                rightPoint: new Point2D
                {
                    X = girderOffset - 2 * Feet,
                    Y = girderOffset - 2 * Feet - cornerJackOffset,
                },
                Justification.Back,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(southwestVerticalCornerJack1);

        var southwestVerticalCornerJack2 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = girderOffset - 4 * Feet, Y = 0 },
                rightPoint: new Point2D
                {
                    X = girderOffset - 4 * Feet,
                    Y = girderOffset - 4 * Feet - cornerJackOffset,
                },
                Justification.Back,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(southwestVerticalCornerJack2);

        var northwestHorizontalCornerJack1 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = 0, Y = Span - girderOffset + 2 * Feet },
                rightPoint: new Point2D
                {
                    X = girderOffset - 2 * Feet - cornerJackOffset,
                    Y = Span - girderOffset + 2 * Feet,
                },
                Justification.Back,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(northwestHorizontalCornerJack1);

        var northwestHorizontalCornerJack2 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = 0, Y = Span - girderOffset + 4 * Feet },
                rightPoint: new Point2D
                {
                    X = girderOffset - 4 * Feet - cornerJackOffset,
                    Y = Span - girderOffset + 4 * Feet,
                },
                Justification.Back,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(northwestHorizontalCornerJack2);

        var northwestVerticalCornerJack1 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = girderOffset - 2 * Feet, Y = Span },
                rightPoint: new Point2D
                {
                    X = girderOffset - 2 * Feet,
                    Y = Span - girderOffset + 2 * Feet + cornerJackOffset,
                },
                Justification.Front,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(northwestVerticalCornerJack1);

        var northwestVerticalCornerJack2 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = girderOffset - 4 * Feet, Y = Span },
                rightPoint: new Point2D
                {
                    X = girderOffset - 4 * Feet,
                    Y = Span - girderOffset + 4 * Feet + cornerJackOffset,
                },
                Justification.Front,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(northwestVerticalCornerJack2);

        var southeastHorizontalCornerJack1 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = BuildingLength, Y = girderOffset - 2 * Feet },
                rightPoint: new Point2D
                {
                    X = BuildingLength - girderOffset + 2 * Feet + cornerJackOffset,
                    Y = girderOffset - 2 * Feet,
                },
                Justification.Back,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(southeastHorizontalCornerJack1);

        var southeastHorizontalCornerJack2 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = BuildingLength, Y = girderOffset - 4 * Feet },
                rightPoint: new Point2D
                {
                    X = BuildingLength - girderOffset + 4 * Feet + cornerJackOffset,
                    Y = girderOffset - 4 * Feet,
                },
                Justification.Back,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(southeastHorizontalCornerJack2);

        var southeastVerticalCornerJack1 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = BuildingLength - girderOffset + 2 * Feet, Y = 0 },
                rightPoint: new Point2D
                {
                    X = BuildingLength - girderOffset + 2 * Feet,
                    Y = girderOffset - 2 * Feet - cornerJackOffset,
                },
                Justification.Front,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(southeastVerticalCornerJack1);

        var southeastVerticalCornerJack2 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = BuildingLength - girderOffset + 4 * Feet, Y = 0 },
                rightPoint: new Point2D
                {
                    X = BuildingLength - girderOffset + 4 * Feet,
                    Y = girderOffset - 4 * Feet - cornerJackOffset,
                },
                Justification.Front,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(southeastVerticalCornerJack2);

        var northeastHorizontalCornerJack1 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = BuildingLength, Y = Span - girderOffset + 2 * Feet },
                rightPoint: new Point2D
                {
                    X = BuildingLength - girderOffset + 2 * Feet + cornerJackOffset,
                    Y = Span - girderOffset + 2 * Feet,
                },
                Justification.Front,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(northeastHorizontalCornerJack1);

        var northeastHorizontalCornerJack2 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = BuildingLength, Y = Span - girderOffset + 4 * Feet },
                rightPoint: new Point2D
                {
                    X = BuildingLength - girderOffset + 4 * Feet + cornerJackOffset,
                    Y = Span - girderOffset + 4 * Feet,
                },
                Justification.Front,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(northeastHorizontalCornerJack2);

        var northeastVerticalCornerJack1 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = BuildingLength - girderOffset + 2 * Feet, Y = Span },
                rightPoint: new Point2D
                {
                    X = BuildingLength - girderOffset + 2 * Feet,
                    Y = Span - girderOffset + 2 * Feet + cornerJackOffset,
                },
                Justification.Back,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(northeastVerticalCornerJack1);

        var northeastVerticalCornerJack2 = await connection.Layouts.CreateTrussEnvelope(
            projectGuid,
            CreateNewTrussEnvelope(
                name: $"{trussEnvelopeNumber++}",
                leftPoint: new Point2D { X = BuildingLength - girderOffset + 4 * Feet, Y = Span },
                rightPoint: new Point2D
                {
                    X = BuildingLength - girderOffset + 4 * Feet,
                    Y = Span - girderOffset + 4 * Feet + cornerJackOffset,
                },
                Justification.Back,
                leftOverhang: OverhangDistance,
                rightOverhang: 0,
                rightBevelCut: new BevelCut { Type = BevelCutType.Double, Angle = 45 }
            )
        );
        trussEnvelopes.Add(northeastVerticalCornerJack2);

        return trussEnvelopes;
    }

    private static NewBearingEnvelope CreateNewBearingEnvelope(
        string name,
        Point2D leftPoint,
        Point2D rightPoint
    ) =>
        new NewBearingEnvelope
        {
            Name = name,
            LeftPoint = leftPoint,
            RightPoint = rightPoint,
            Thickness = 3.5,
            Top = WallTop,
            Bottom = 0,
            Justification = Justification.Front,
        };

    private static NewRoofPlane CreateNewRoofPlane(Guid bearingEnvelopeGuid)
    {
        var slopeInRadians = SlopeToRadians(4);

        var buttCut = 0.25;
        var topChordLumberWidth = 3.5;
        var heelHeight = buttCut + topChordLumberWidth / Math.Cos(slopeInRadians);

        return new NewRoofPlane
        {
            GeometryType = RoofPlaneReferenceGeometryType.BearingEnvelope,
            BearingEnvelopeGuid = bearingEnvelopeGuid,
            Slope = RadiansToDegrees(slopeInRadians),
            HeelHeight = heelHeight,
            Overhang = OverhangDistance,
        };
    }

    private static IEnumerable<PlaneCut> CreatePlaneCuts(IEnumerable<RoofPlane> roofPlanes) =>
        roofPlanes.Select(CreatePlaneCut);

    private static PlaneCut CreatePlaneCut(RoofPlane roofPlane) =>
        new PlaneCut { Type = PlaneCutType.AgainstPlane, CuttingPlaneGuid = roofPlane.Guid };

    private static NewTrussEnvelope CreateNewTrussEnvelope(
        string name,
        Point2D leftPoint,
        Point2D rightPoint,
        Justification justification,
        double leftOverhang,
        double rightOverhang,
        BevelCut? leftBevelCut = null,
        BevelCut? rightBevelCut = null
    ) =>
        new NewTrussEnvelope
        {
            Name = name,
            LeftPoint = leftPoint,
            RightPoint = rightPoint,
            Elevation = WallTop,
            Justification = justification,
            Thickness = 1.5,
            LeftOverhang = leftOverhang,
            RightOverhang = rightOverhang,
            LeftBevelCut = leftBevelCut,
            RightBevelCut = rightBevelCut,
        };

    private static double SlopeToRadians(double slope) => Math.Atan(slope / 12);

    private static double RadiansToDegrees(double radians) => radians / Math.PI * 180;
}
