using System;

namespace gspro_r10
{
    public readonly record struct TrajectoryResult(
        double CarryYards,
        double ApexYards,
        double FlightTimeSeconds,
        double LandingSpeedMph);

    public static class CarryCalculator
    {
        // Numerical golf-ball flight model. This is an estimate, not a replacement for
        // the R10's measured carry/flight data. It models gravity, aerodynamic drag and
        // Magnus lift from backspin using small time steps until the ball reaches ground.
        public static TrajectoryResult Calculate(
            double ballSpeedMph,
            double launchAngleDeg,
            double backSpinRpm,
            double airDensityKgM3 = 1.225)
        {
            if (ballSpeedMph <= 1 || launchAngleDeg <= -5 || launchAngleDeg >= 70)
                return default;

            const double massKg = 0.04593;
            const double diameterM = 0.04267;
            const double areaM2 = Math.PI * diameterM * diameterM / 4.0;
            const double gravity = 9.80665;
            const double dt = 0.0025;

            double speed = ballSpeedMph * 0.44704;
            double angle = launchAngleDeg * Math.PI / 180.0;
            double vx = speed * Math.Cos(angle);
            double vz = speed * Math.Sin(angle);
            double x = 0;
            double z = 0;
            double time = 0;
            double apex = 0;
            double previousX = 0;
            double previousZ = 0;
            double previousTime = 0;

            // Golf-ball spin parameter. A smooth empirical lift relationship avoids the
            // instability of treating lift coefficient as directly proportional to RPM.
            double spinRateRadS = Math.Max(0, backSpinRpm) * 2.0 * Math.PI / 60.0;
            double spinParameter = spinRateRadS * (diameterM / 2.0) / Math.Max(speed, 1.0);
            double cl = 0.20 + 0.55 * spinParameter;
            cl = Math.Clamp(cl, 0.05, 0.42);

            // A modest speed-dependent drag coefficient representative of a dimpled golf ball.
            double cd = 0.21 + 0.08 / (1.0 + speed / 45.0);
            airDensityKgM3 = Math.Clamp(airDensityKgM3, 0.8, 1.5);

            for (int i = 0; i < 12000; i++)
            {
                previousX = x;
                previousZ = z;
                previousTime = time;

                double v = Math.Sqrt(vx * vx + vz * vz);
                if (v < 0.5)
                    break;

                double dynamicPressure = 0.5 * airDensityKgM3 * v * v;
                double dragForce = dynamicPressure * cd * areaM2;
                double liftForce = dynamicPressure * cl * areaM2;

                // Drag opposes velocity. Lift is perpendicular to the velocity and points
                // upward for backspin in this 2-D vertical trajectory.
                double ax = -dragForce * vx / (massKg * v) - liftForce * vz / (massKg * v);
                double az = -gravity - dragForce * vz / (massKg * v) + liftForce * vx / (massKg * v);

                vx += ax * dt;
                vz += az * dt;
                x += vx * dt;
                z += vz * dt;
                time += dt;

                if (z > apex)
                    apex = z;

                if (z < 0 && time > dt)
                {
                    // Linear interpolation between the last above-ground point and the
                    // ground crossing gives a better carry distance than using the raw step.
                    double dz = z - previousZ;
                    double fraction = Math.Abs(dz) > 1e-9 ? (0 - previousZ) / dz : 0;
                    fraction = Math.Clamp(fraction, 0, 1);
                    double landingX = previousX + (x - previousX) * fraction;
                    double landingTime = previousTime + (time - previousTime) * fraction;
                    double landingVx = vx;
                    double landingVz = vz;
                    double landingSpeedMph = Math.Sqrt(landingVx * landingVx + landingVz * landingVz) / 0.44704;

                    return new TrajectoryResult(
                        landingX * 1.0936133,
                        apex * 1.0936133,
                        landingTime,
                        landingSpeedMph);
                }
            }

            return new TrajectoryResult(
                Math.Max(0, x * 1.0936133),
                Math.Max(0, apex * 1.0936133),
                time,
                Math.Sqrt(vx * vx + vz * vz) / 0.44704);
        }

        public static double EstimateCarryYards(double ballSpeedMph, double launchAngleDeg, double backSpinRpm)
            => Calculate(ballSpeedMph, launchAngleDeg, backSpinRpm).CarryYards;
    }
}
