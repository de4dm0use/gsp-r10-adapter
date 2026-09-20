using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace gspro_r10
{
    public readonly record struct TracerShot(
        double CarryYards,
        double ApexYards,
        double FlightTimeSeconds,
        double LaunchAngleDeg,
        double LaunchDirectionDeg,
        double SpinAxisDeg);

    public static class TracerEngine
    {
        public static IReadOnlyList<Point> BuildScreenPath(int width, int height, TracerShot shot)
        {
            var points = new List<Point>(41);
            double startX = width * 0.18;
            double groundY = height * 0.84;
            double endX = width * (0.78 + Math.Clamp(shot.LaunchDirectionDeg / 45.0, -0.10, 0.10));
            endX = Math.Clamp(endX, width * 0.58, width * 0.92);

            double carryFactor = Math.Clamp(shot.CarryYards / 280.0, 0.45, 1.15);
            double apexRatio = Math.Clamp(0.16 + shot.ApexYards / 180.0 * 0.28 * carryFactor, 0.16, 0.46);
            double apexY = height * apexRatio;

            double curve = Math.Clamp(shot.SpinAxisDeg / 25.0, -0.12, 0.12) * width;
            for (int i = 0; i <= 40; i++)
            {
                double t = i / 40.0;
                double x = startX + (endX - startX) * t + curve * t * t;
                double arc = Math.Sin(Math.PI * t);
                double y = groundY + (apexY - groundY) * arc;
                points.Add(new Point((int)x, (int)y));
            }

            return points;
        }

        public static void Draw(Mat frame, TracerShot shot, double progress, bool showLandingMarker = true)
        {
            var path = BuildScreenPath(frame.Width, frame.Height, shot);
            int visible = Math.Clamp((int)Math.Round(progress * (path.Count - 1)), 1, path.Count - 1);

            for (int i = 1; i <= visible; i++)
            {
                int thickness = 6;
                Cv2.Line(frame, path[i - 1], path[i], new Scalar(255, 185, 25), thickness, LineTypes.AntiAlias);
                if (i > 3)
                    Cv2.Line(frame, path[i - 3], path[i], new Scalar(255, 255, 255), 2, LineTypes.AntiAlias);
            }

            var ball = path[visible];
            Cv2.Circle(frame, ball, 9, new Scalar(255, 255, 255), -1, LineTypes.AntiAlias);
            Cv2.Circle(frame, ball, 13, new Scalar(255, 185, 25), 2, LineTypes.AntiAlias);

            if (showLandingMarker && progress >= 1.0)
            {
                var landing = path[^1];
                Cv2.Circle(frame, landing, 18, new Scalar(255, 185, 25), 2, LineTypes.AntiAlias);
                Cv2.Line(frame, new Point(landing.X - 12, landing.Y), new Point(landing.X + 12, landing.Y), new Scalar(255, 185, 25), 2, LineTypes.AntiAlias);
                Cv2.Line(frame, new Point(landing.X, landing.Y - 12), new Point(landing.X, landing.Y + 12), new Scalar(255, 185, 25), 2, LineTypes.AntiAlias);
            }
        }

        public static void DrawHud(Mat frame, TracerShot shot)
        {
            string line = $"Carry {shot.CarryYards:0} yd   Apex {shot.ApexYards:0.0} yd   Launch {shot.LaunchAngleDeg:0.0}°   Flight {shot.FlightTimeSeconds:0.00}s";
            Cv2.Rectangle(frame, new Rect(18, 18, Math.Min(frame.Width - 36, 700), 42), new Scalar(15, 20, 25), -1);
            Cv2.PutText(frame, line, new Point(30, 47), HersheyFonts.HersheySimplex, 0.65, new Scalar(245, 245, 245), 2, LineTypes.AntiAlias);
        }
    }
}
