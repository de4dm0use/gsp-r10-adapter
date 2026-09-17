namespace gspro_r10
{
  public static class CarryCalculator
  {
    // R10 does not send a direct carry-distance field. This is a physics-based estimate
    // using ball speed, launch angle and spin. It is intentionally labelled as estimated.
    public static double EstimateCarryYards(double ballSpeedMph, double launchAngleDeg, double spinRpm)
    {
      if (ballSpeedMph <= 1 || launchAngleDeg <= -5 || launchAngleDeg >= 60) return 0;

      double v = ballSpeedMph * 0.44704;
      double angle = launchAngleDeg * Math.PI / 180.0;
      double vx = v * Math.Cos(angle);
      double vy = v * Math.Sin(angle);
      double x = 0, y = 0;
      const double dt = 0.01;
      const double g = 9.80665;
      const double mass = 0.04593;
      const double area = 0.001432;
      const double rho = 1.225;
      const double cd = 0.255;

      // Approximate lift coefficient from spin parameter, clamped to keep the model stable.
      double spinParameter = spinRpm > 0 ? (spinRpm * 2 * Math.PI / 60.0) * 0.02135 / Math.Max(v, 1) : 0;
      double cl = Math.Clamp(1.8 * spinParameter, 0, 0.32);

      double lastX = 0;
      for (int i = 0; i < 3000 && y >= -0.05; i++)
      {
        lastX = x;
        double speed = Math.Sqrt(vx * vx + vy * vy);
        if (speed < 1) break;
        double drag = 0.5 * rho * cd * area * speed * speed / mass;
        double lift = 0.5 * rho * cl * area * speed * speed / mass;
        double ax = -drag * vx / speed - lift * vy / speed;
        double ay = -g - drag * vy / speed + lift * vx / speed;
        vx += ax * dt;
        vy += ay * dt;
        x += vx * dt;
        y += vy * dt;
      }

      return Math.Max(0, lastX * 1.0936133);
    }
  }
}
