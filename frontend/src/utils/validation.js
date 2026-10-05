const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const COLOR_PATTERN = /^#[0-9a-fA-F]{6}$/;

// Samma gränser som SubscriptionRequest.MinDate/MaxDate i API:t.
export const MIN_DATE = "2000-01-01";
export const MAX_DATE = "2100-12-31";

//----------
//-----Rules
//----------

const EMAIL_RULES = [
  { test: (v) => v.email.trim() !== "", message: "Ange din e-postadress." },
  {
    test: (v) => EMAIL_PATTERN.test(v.email),
    message: "Ange en giltig e-postadress.",
  },
];

const PASSWORD_RULES = [
  {
    test: (v) => v.password.length >= 8,
    message: "Lösenordet måste vara minst 8 tecken.",
  },
  {
    test: (v) => /\d/.test(v.password),
    message: "Lösenordet måste innehålla minst en siffra.",
  },
  {
    test: (v) => /\p{Ll}/u.test(v.password),
    message: "Lösenordet måste innehålla minst en liten bokstav.",
  },
  {
    test: (v) => /\p{Lu}/u.test(v.password),
    message: "Lösenordet måste innehålla minst en stor bokstav.",
  },
];

const LOGIN_RULES = {
  email: EMAIL_RULES,
  password: [
    { test: (v) => v.password !== "", message: "Ange ditt lösenord." },
  ],
};

const REGISTER_RULES = {
  email: EMAIL_RULES,
  password: PASSWORD_RULES,
  confirmPassword: [
    {
      test: (v) => v.confirmPassword === v.password,
      message: "Lösenorden matchar inte.",
    },
  ],
};

const SUBSCRIPTION_RULES = {
  name: [
    { test: (v) => v.name.trim() !== "", message: "Ange ett namn." },
    {
      test: (v) => v.name.length <= 100,
      message: "Namnet får vara högst 100 tecken.",
    },
  ],
  categoryId: [
    { test: (v) => v.categoryId !== "", message: "Välj en kategori." },
  ],
  price: [
    { test: (v) => v.price !== "", message: "Ange ett pris." },
    {
      test: (v) => isBetween(Number(v.price), 0, 100000),
      message: "Priset måste vara mellan 0 och 100 000 kr.",
    },
  ],
  billingInterval: [
    {
      test: (v) => v.billingInterval !== "",
      message: "Välj ett betalningsintervall.",
    },
  ],
  startDate: [
    { test: (v) => v.startDate !== "", message: "Ange ett startdatum." },
    {
      test: (v) => isBetween(v.startDate, MIN_DATE, MAX_DATE),
      message: `Startdatumet måste vara mellan ${MIN_DATE} och ${MAX_DATE}.`,
    },
  ],
  nextPaymentDate: [
    {
      test: (v) => v.nextPaymentDate !== "",
      message: "Ange nästa betalningsdatum.",
    },
    {
      test: (v) => isBetween(v.nextPaymentDate, MIN_DATE, MAX_DATE),
      message: `Nästa betalning måste vara mellan ${MIN_DATE} och ${MAX_DATE}.`,
    },
    {
      test: (v) => !v.startDate || v.nextPaymentDate >= v.startDate,
      message: "Nästa betalning kan inte vara före startdatumet.",
    },
  ],
  notes: [
    {
      test: (v) => v.notes.length <= 500,
      message: "Anteckningen får vara högst 500 tecken.",
    },
  ],
};

const CATEGORY_RULES = {
  name: [
    { test: (v) => v.name.trim() !== "", message: "Ange ett namn." },
    {
      test: (v) => v.name.length <= 50,
      message: "Namnet får vara högst 50 tecken.",
    },
  ],
  color: [
    { test: (v) => COLOR_PATTERN.test(v.color), message: "Välj en färg." },
  ],
};

//---------------
//-----Validators
//---------------

export const validateLogin = (values) => validate(values, LOGIN_RULES);

export const validateRegister = (values) => validate(values, REGISTER_RULES);

export const validateSubscription = (values) =>
  validate(values, SUBSCRIPTION_RULES);

export const validateCategory = (values) => validate(values, CATEGORY_RULES);

//------------
//-----Helpers
//------------

export function hasErrors(errors) {
  return Object.keys(errors).length > 0;
}

function validate(values, rules) {
  const errors = {};

  for (const [field, fieldRules] of Object.entries(rules)) {
    const failedRule = fieldRules.find((rule) => !rule.test(values));

    if (failedRule) {
      errors[field] = failedRule.message;
    }
  }

  return errors;
}

function isBetween(value, min, max) {
  return value >= min && value <= max;
}
