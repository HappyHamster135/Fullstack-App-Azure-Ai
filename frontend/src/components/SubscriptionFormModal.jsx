import { Alert, Button, Col, Form, Modal, Row } from "react-bootstrap";
import { Link } from "react-router";
import { useForm } from "../hooks/useForm.js";
import { BILLING_INTERVALS } from "../utils/billingIntervals.js";
import { toIsoDate } from "../utils/format.js";
import { MAX_DATE, MIN_DATE, validateSubscription } from "../utils/validation.js";
import FormField from "./FormField.jsx";

//----------------
//-----Form values
//----------------

function toFormValues(subscription) {
  if (!subscription) {
    const today = toIsoDate(new Date());

    return {
      name: "",
      categoryId: "",
      price: "",
      billingInterval: "Monthly",
      startDate: today,
      nextPaymentDate: today,
      isActive: true,
      notes: "",
    };
  }

  return {
    name: subscription.name,
    categoryId: String(subscription.category.id),
    price: String(subscription.price),
    billingInterval: subscription.billingInterval,
    startDate: subscription.startDate,
    nextPaymentDate: subscription.nextPaymentDate,
    isActive: subscription.isActive,
    notes: subscription.notes ?? "",
  };
}

function toRequest(values) {
  return {
    ...values,
    name: values.name.trim(),
    price: Number(values.price),
    categoryId: Number(values.categoryId),
    notes: values.notes.trim() || null,
  };
}

//----------
//-----Modal
//----------

function SubscriptionFormModal({ subscription, categories, onSave, onClose }) {
  const {
    values,
    errors,
    serverError,
    isSubmitting,
    handleChange,
    handleSubmit,
  } = useForm(
    toFormValues(subscription),
    validateSubscription,
    async (formValues) => {
      await onSave(toRequest(formValues));
      onClose();
    },
  );

  const categoryOptions = [
    { value: "", label: "Välj kategori…" },
    ...categories.map((category) => ({
      value: String(category.id),
      label: category.name,
    })),
  ];

  const field = (name) => ({
    name,
    value: values[name],
    error: errors[name],
    onChange: handleChange,
  });

  return (
    <Modal show onHide={onClose} centered size="lg">
      <Form noValidate onSubmit={handleSubmit}>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">
            {subscription ? "Redigera prenumeration" : "Ny prenumeration"}
          </Modal.Title>
        </Modal.Header>

        <Modal.Body>
          {serverError && <Alert variant="danger">{serverError}</Alert>}

          {categories.length === 0 && (
            <Alert variant="warning">
              Du har inga kategorier ännu.{" "}
              <Link to="/categories">Skapa en kategori</Link> först.
            </Alert>
          )}

          <Row>
            <Col md={7}>
              <FormField label="Namn" {...field("name")} />
            </Col>
            <Col md={5}>
              <FormField
                label="Kategori"
                options={categoryOptions}
                {...field("categoryId")}
              />
            </Col>
          </Row>

          <Row>
            <Col sm={6}>
              <FormField
                label="Pris (kr)"
                type="number"
                min="0"
                step="0.01"
                inputMode="decimal"
                {...field("price")}
              />
            </Col>
            <Col sm={6}>
              <FormField
                label="Betalningsintervall"
                options={BILLING_INTERVALS}
                {...field("billingInterval")}
              />
            </Col>
          </Row>

          <Row>
            <Col sm={6}>
              <FormField
                label="Startdatum"
                type="date"
                min={MIN_DATE}
                max={MAX_DATE}
                {...field("startDate")}
              />
            </Col>
            <Col sm={6}>
              <FormField
                label="Nästa betalning"
                type="date"
                min={MIN_DATE}
                max={MAX_DATE}
                {...field("nextPaymentDate")}
              />
            </Col>
          </Row>

          <FormField
            label="Anteckning"
            type="textarea"
            rows={2}
            {...field("notes")}
          />

          <Form.Check
            type="switch"
            id="isActive"
            name="isActive"
            label="Aktiv prenumeration"
            checked={values.isActive}
            onChange={handleChange}
          />
        </Modal.Body>

        <Modal.Footer>
          <Button variant="outline-secondary" onClick={onClose}>
            Avbryt
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Sparar…" : "Spara"}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  );
}

export default SubscriptionFormModal;
