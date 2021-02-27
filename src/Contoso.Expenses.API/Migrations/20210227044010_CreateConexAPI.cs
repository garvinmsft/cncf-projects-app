using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Contoso.Expenses.API.Migrations
{
    public partial class CreateConexAPI : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostCenters",
                columns: table => new
                {
                    CostCenterId = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SubmitterEmail = table.Column<string>(nullable: true),
                    ApproverEmail = table.Column<string>(nullable: true),
                    CostCenterName = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCenters", x => x.CostCenterId);
                });
                
            migrationBuilder.InsertData(
                table: "CostCenters",
                columns: new[] { "CostCenterId", "SubmitterEmail", "ApproverEmail", "CostCenterName" },
                values: new object[] {1, "user1@mycompany.com", "user1@mycompany.com", "123E42"});

            migrationBuilder.InsertData(
                table: "CostCenters",
                columns: new[] { "CostCenterId", "SubmitterEmail", "ApproverEmail", "CostCenterName" },
                values: new object[] {2, "user2@mycompany.com", "user2@mycompany.com", "456C14"});

            migrationBuilder.InsertData(
                table: "CostCenters",
                columns: new[] { "CostCenterId", "SubmitterEmail", "ApproverEmail", "CostCenterName" },
                values: new object[] {3, "user3@mycompany.com", "user3@mycompany.com", "456C14"});
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostCenters");
        }
    }
}
