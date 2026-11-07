import { z } from "zod";

const id = z.guid();
const instant = z.iso.datetime({ offset: true });
export const sessionSchema = z.object({
  id,
  displayName: z.string(),
  memberships: z.array(
    z.object({
      organizationId: id,
      organizationName: z.string(),
      permission: z.enum(["Manager", "Employee"]),
    }),
  ),
});
export const workspaceSchema = z.object({
  organizationName: z.string(),
  permission: z.enum(["Manager", "Employee"]),
  suggestedWeek: z.iso.date(),
  locations: z.array(
    z.object({ id, name: z.string(), timeZoneId: z.string() }),
  ),
  skills: z.array(z.object({ id, name: z.string() })),
  people: z.array(
    z.object({ id, displayName: z.string(), maxWeeklyHours: z.number() }),
  ),
  qualifications: z.array(z.object({ userId: id, skillId: id })),
});
export const shiftsSchema = z.array(
  z.object({
    id,
    locationId: id,
    skillId: id,
    startsAt: instant,
    endsAt: instant,
    assigneeUserId: id.nullable(),
    assigneeName: z.string().nullable(),
    version: id,
  }),
);
export const availabilitySchema = z.array(
  z.object({
    id,
    userId: id,
    locationId: id,
    startsAt: instant,
    endsAt: instant,
  }),
);
export const requestsSchema = z.array(
  z.object({
    id,
    shiftId: id,
    requesterUserId: id,
    requesterName: z.string(),
    status: z.literal("Pending"),
    createdAt: instant,
    startsAt: instant,
    endsAt: instant,
    skillId: id,
  }),
);
export const gapsSchema = z.array(
  z.object({
    requirementId: id,
    locationId: id,
    skillId: id,
    startsAt: instant,
    endsAt: instant,
    requiredCount: z.number(),
    assignedCount: z.number(),
    missingCount: z.number(),
  }),
);
export const requirementsSchema = z.array(
  z.object({
    id,
    locationId: id,
    skillId: id,
    startsAt: instant,
    endsAt: instant,
    requiredCount: z.number(),
  }),
);

export type Session = z.infer<typeof sessionSchema>;
export type Workspace = z.infer<typeof workspaceSchema>;
export type Shift = z.infer<typeof shiftsSchema>[number];
export type Availability = z.infer<typeof availabilitySchema>[number];
export type CoverageRequest = z.infer<typeof requestsSchema>[number];
export type Gap = z.infer<typeof gapsSchema>[number];
export type Requirement = z.infer<typeof requirementsSchema>[number];
