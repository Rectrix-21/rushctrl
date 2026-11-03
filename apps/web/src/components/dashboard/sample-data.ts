export const sampleShifts = [
  {
    initials: "JL",
    name: "Jamie Lee",
    role: "Shift lead",
    start: "06:00",
    end: "14:00",
    detail: "Opening lead",
    column: "2 / 6",
    later: false,
  },
  {
    initials: "MP",
    name: "Morgan Park",
    role: "Barista",
    start: "08:00",
    end: "16:00",
    detail: "Espresso bar",
    column: "3 / 7",
    later: false,
  },
  {
    initials: "AR",
    name: "Alex Rivera",
    role: "Barista",
    start: "10:00",
    end: "18:00",
    detail: "Espresso bar",
    column: "4 / 8",
    later: true,
  },
  {
    initials: "SC",
    name: "Sam Chen",
    role: "Floor team",
    start: "12:00",
    end: "20:00",
    detail: "Front of house",
    column: "5 / 9",
    later: true,
  },
] as const;

export const sampleRequests = [
  {
    initials: "MP",
    name: "Morgan Park",
    description: "Coverage request · Sat, Nov 7",
  },
  {
    initials: "AR",
    name: "Alex Rivera",
    description: "Availability update · next week",
  },
] as const;
