const MESSAGES = {
  network: 'Kunde inte nå servern. Kontrollera anslutningen och försök igen.',
  validation: 'Kontrollera de markerade fälten.',
  unknown: 'Något gick fel. Försök igen.',
}

// Anrop som avbröts med en AbortController är inget fel utan ett medvetet avbrott.
export function isCanceled(error) {
  return error?.code === 'ERR_CANCELED'
}

export function getErrorMessage(error) {
  if (!error.response) {
    return MESSAGES.network
  }

  const { data } = error.response

  if (data?.errors) {
    return MESSAGES.validation
  }

  return data?.detail ?? MESSAGES.unknown
}

export function getFieldErrors(error) {
  const errors = error.response?.data?.errors ?? {}

  return Object.fromEntries(
    Object.entries(errors).map(([field, messages]) => [toCamelCase(field), messages[0]]),
  )
}

function toCamelCase(value) {
  return value.charAt(0).toLowerCase() + value.slice(1)
}
