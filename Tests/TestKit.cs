﻿using System;
using System.Collections.Generic;
using System.Linq;
using Speckle.Kits;
using Speckle.Models;

namespace Tests
{
  public class CarpentersTestKit : ISpeckleKit
  {
    public IEnumerable<Type> Types => GetType().Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(Base)));

    public IEnumerable<Type> Converters => GetType().Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(Converter)));

    public string Description => "Simple object model for basic geometry types.";

    public string Name => nameof(CarpentersTestKit);

    public string Author => "Dimitrie";

    public string WebsiteOrEmail => "hello@speckle.works";

    public CarpentersTestKit() { }
  }

  public class DiningTable : Base
  {
    [DetachProperty]
    public TableLeg LegOne { get; set; }

    [DetachProperty]
    public TableLeg LegTwo { get; set; }

    [DetachProperty]
    public List<TableLeg> MoreLegs { get; set; } = new List<TableLeg>();

    [DetachProperty]
    public Tabletop Tabletop { get; set; }

    public string TableModel { get; set; } = "Sample Table";

    public override string hash => "table_" + base.hash;

    public DiningTable()
    {
      LegOne = new TableLeg() { height = 2 * 3, radius = 10 };
      LegTwo = new TableLeg() { height = 1, radius = 5 };

      MoreLegs.Add(new TableLeg() { height = 4 });
      MoreLegs.Add(new TableLeg() { height = 10 });

      Tabletop = new Tabletop() { length = 200, width = 12, thickness = 3 };

      TableModel += " :: " + DateTime.Now;
    }
  }

  public class Tabletop : Base
  {
    public double length { get; set; }
    public double width { get; set; }
    public double thickness { get; set; }
    public override string hash => "table_top_" + base.hash;
    public Tabletop() { }
  }

  public class TableLeg : Base
  {
    public double height { get; set; }
    public double radius { get; set; }

    [DetachProperty]
    public TableLegFixture fixture { get; set; } = new TableLegFixture();

    public TableLeg() { }

    public override string hash => "table_leg_" + base.hash;
  }

  public class TableLegFixture : Base
  {
    public string nails { get; set; } = "MANY NAILS WOW " + DateTime.Now;
    public override string hash => "table_leg_fixture_" + base.hash;
    public TableLegFixture() { }
  }
}