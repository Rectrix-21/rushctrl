import { z } from "zod";
const local = z
  .string()
  .regex(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/, "Enter a local date and time.")
  .refine(
    (value) =>
      z.iso.date().safeParse(value.slice(0, 10)).success &&
      /^([01]\d|2[0-3]):[0-5]\d$/.test(value.slice(11)),
    "Enter a valid local date and time.",
  );
export const intervalForm = z
  .object({ locationId: z.guid(), startsAt: local, endsAt: local })
  .refine((value) => value.endsAt > value.startsAt, {
    message: "End time must be after start time.",
    path: ["endsAt"],
  });
export const shiftForm = intervalForm.safeExtend({ skillId: z.guid() });
export const requirementForm = shiftForm.safeExtend({
  requiredCount: z.coerce
    .number()
    .int()
    .min(1, "Enter at least one person.")
    .max(100),
});
export const loginForm = z.object({
  email: z.email("Enter a valid email address.").max(256),
  password: z.string().min(1, "Enter your password.").max(256),
});
