Feature: Explore service
  As a Stylora user
  I want catalogue searches filtered to my palette and free of irrelevant items
  So that the explore page shows products worth my attention

  Scenario: Excluded product types are filtered out
    Given the product catalogue returns:
      | id | name        | colour |
      | 1  | Linen Shirt | white  |
      | 2  | Bikini Set  | black  |
    When products are searched on page 1 with page size 20
    Then only the product ids "1" are returned
    And the catalogue was queried exactly 1 time

  Scenario: Palette filtering keeps only matching colours
    Given the product catalogue returns:
      | id | name          | colour |
      | 1  | Rust Knit     | orange |
      | 2  | Cobalt Jumper | blue   |
    And the shopper palette is "#FF8C00"
    When products are searched on page 1 with page size 20
    Then only the product ids "1" are returned
    And every returned product is a palette match

  Scenario Outline: Page size is clamped to the supported range
    Given the product catalogue is empty
    When products are searched on page 1 with page size <requested>
    Then the result page size is <effective>

    Examples:
      | requested | effective |
      | 0         | 1         |
      | 100       | 48        |
      | 20        | 20        |
