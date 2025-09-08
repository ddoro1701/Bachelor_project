import React, { useState, useEffect, useMemo } from 'react';
import './EmailSelector.css';

const EmailSelector = ({ ocrText, fetchPackages }) => {
  const [emails, setEmails] = useState([]);
  const [fullName, setFullName] = useState('');
  const [newEmail, setNewEmail] = useState('');
  const [selectedEmail, setSelectedEmail] = useState('');
  const [recognizedEmail, setRecognizedEmail] = useState('');
  const [shippingProvider, setShippingProvider] = useState(() => localStorage.getItem('shippingProvider') || 'Amazon');
  const [itemCount, setItemCount] = useState(() => Number(localStorage.getItem('itemCount') || 1));
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // Neu: Typeahead State
  const [showSuggestions, setShowSuggestions] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);

  const suggestions = useMemo(() => {
    const q = (selectedEmail || '').toLowerCase();
    if (!q) return emails.slice(0, 8);
    return emails.filter(e => e.toLowerCase().includes(q)).slice(0, 8);
  }, [emails, selectedEmail]);

  // Load lecturer emails when component mounts.
  useEffect(() => {
    fetch('https://wrexhamuni-ocr-webapp-deeaeydrf2fdcfdy.uksouth-01.azurewebsites.net/api/lecturer/emails')
      .then(res => {
        if (!res.ok) throw new Error("Error fetching emails");
        return res.json();
      })
      .then(data => {
        setEmails(data);
        if (data.length > 0) setSelectedEmail(data[0]);
      })
      .catch(err => console.error('Error fetching emails:', err));
  }, []);

  // Call lecturer matcher when OCR text changes.
  useEffect(() => {
    console.log("[EmailSelector] useEffect triggered with OCR text:", ocrText);
    if (ocrText && ocrText.trim()) {
      setLoading(true); // Start loading
      setError(''); // Clear previous errors
      console.log("Calling /api/label/find-email with OCR text:", ocrText);

      fetch('https://wrexhamuni-ocr-webapp-deeaeydrf2fdcfdy.uksouth-01.azurewebsites.net/api/label/find-email', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(ocrText),
      })
        .then(res => {
          if (res.status === 404) {
            // Endpoint (noch) nicht vorhanden im Deployment
            setRecognizedEmail('');
            setError('No matching lecturer email found.');
            window.dispatchEvent(new CustomEvent('toast', { detail: { type: 'info', message: 'No lecturer suggestion found.' } }));
            return null;
          }
          if (!res.ok) throw new Error('No matching lecturer email found.');
          return res.json();
        })
        .then(data => {
          if (data && data.email) setRecognizedEmail(data.email);
        })
        .catch(() => {
          setError('No matching lecturer email found.');
        })
        .finally(() => setLoading(false));
    }
  }, [ocrText]);

  const handleSendEmail = () => {
    if (!recognizedEmail) {
      window.dispatchEvent(new CustomEvent('notice', {
        detail: { type: 'error', message: 'No suggested lecturer found.' }
      }));
      return;
    }
    const packageData = {
      LecturerEmail: recognizedEmail,
      ItemCount: parseInt(itemCount, 10),
      ShippingProvider: shippingProvider,
      AdditionalInfo: providerDescription,
      CollectionDate: new Date(),
    };
    fetch('https://wrexhamuni-ocr-webapp-deeaeydrf2fdcfdy.uksouth-01.azurewebsites.net/api/package/send-email', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(packageData),
    })
      .then(res => {
        if (!res.ok) throw new Error('Error sending package data');
        return res.json();
      })
      .then(() => {
        window.dispatchEvent(new CustomEvent('notice', {
          detail: { type: 'success', message: 'Email sent to', lecturer: recognizedEmail }
        }));
        fetchPackages();
        setItemCount(1);
        setProviderDescription('');
      })
      .catch(() => {
        window.dispatchEvent(new CustomEvent('notice', {
          detail: { type: 'error', message: 'Failed to create package record.' }
        }));
      });
  };

  const handleAddEmail = () => {
    if (!newEmail.trim() || !fullName.trim()) {
      window.dispatchEvent(new CustomEvent('toast', { detail: { type: 'info', message: 'Please provide full name and email.' } }));
      return;
    }
    const lecturerData = { Name: fullName, Email: newEmail };
    fetch('https://wrexhamuni-ocr-webapp-deeaeydrf2fdcfdy.uksouth-01.azurewebsites.net/api/lecturer/emails', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(lecturerData)
    })
      .then(res => {
        if (!res.ok) throw new Error('Fehler beim Hinzufügen der E-Mail');
        return res.json();
      })
      .then(addedLecturer => {
        setEmails(prev => [...prev, addedLecturer.Email]);
        setSelectedEmail(addedLecturer.Email);
        setFullName('');
        setNewEmail('');
        window.dispatchEvent(new CustomEvent('toast', { detail: { type: 'success', message: 'Lecturer added.' } }));
      })
      .catch(() => window.dispatchEvent(new CustomEvent('toast', { detail: { type: 'error', message: 'Failed to add lecturer.' } })));
  };

  const handleDeleteEmail = () => {
    if (!selectedEmail) return;
    fetch(`https://wrexhamuni-ocr-webapp-deeaeydrf2fdcfdy.uksouth-01.azurewebsites.net/api/lecturer/emails?email=${encodeURIComponent(selectedEmail)}`, {
      method: 'DELETE'
    })
      .then(res => {
        if (!res.ok) throw new Error('Error deleting email');
        return res.json();
      })
      .then(() => {
        setEmails(prev => prev.filter(email => email !== selectedEmail));
        const remaining = emails.filter(email => email !== selectedEmail);
        setSelectedEmail(remaining.length > 0 ? remaining[0] : '');
      })
      .catch(err => console.error('Error deleting email:', err));
  };

  const handleSendEmailWithChosenEmail = () => {
    const packageData = {
      LecturerEmail: selectedEmail,
      ItemCount: parseInt(itemCount, 10),
      ShippingProvider: shippingProvider,
      AdditionalInfo: providerDescription,
      CollectionDate: new Date(),
    };
    fetch('https://wrexhamuni-ocr-webapp-deeaeydrf2fdcfdy.uksouth-01.azurewebsites.net/api/package/send-email', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(packageData),
    })
      .then(res => {
        if (!res.ok) throw new Error('Error sending package data with chosen email');
        return res.json();
      })
      .then(() => {
        window.dispatchEvent(new CustomEvent('notice', {
          detail: { type: 'success', message: 'Email sent to', lecturer: selectedEmail }
        }));
        fetchPackages();
        setItemCount(1);
        setProviderDescription('');
      })
      .catch(() => {
        window.dispatchEvent(new CustomEvent('notice', {
          detail: { type: 'error', message: 'Failed to create package record.' }
        }));
      });
  };

  useEffect(() => { localStorage.setItem('shippingProvider', shippingProvider); }, [shippingProvider]);
  useEffect(() => { localStorage.setItem('itemCount', String(itemCount)); }, [itemCount]);

  return (
    <div className="email-selector">
      <div className="send-email-section">
        {loading && <p className="status-info">Processing... Please wait.</p>}
        {error && <p className="status-error">{error}</p>}
        {!loading && recognizedEmail && (
          <p className="status-info">
            Suggested Lecturer Email: {recognizedEmail}{' '}
            <button
              className="btn"
              onClick={() => { navigator.clipboard.writeText(recognizedEmail); window.dispatchEvent(new CustomEvent('toast',{detail:{type:'info',message:'Copied to clipboard'}})); }}
              style={{ marginLeft: 8 }}
            >
              Copy
            </button>
          </p>
        )}
        <button onClick={handleSendEmail}>Send Email/Log Information</button>
      </div>

      {/* Email selection and New Lecturer section */}
      <div className="email-container">
        <div className="email-select">
          <h2>Choose an Email</h2>

          {/* Typeahead statt <select> */}
          <div
            className="typeahead"
            role="combobox"
            aria-expanded={showSuggestions}
            aria-owns="email-suggestions"
            aria-haspopup="listbox"
          >
            <input
              type="text"
              className="typeahead-input"
              placeholder="Start typing an email..."
              value={selectedEmail}
              onChange={(e) => {
                setSelectedEmail(e.target.value);
                setShowSuggestions(true);
                setActiveIndex(-1);
              }}
              onFocus={() => setShowSuggestions(true)}
              onBlur={() => setTimeout(() => setShowSuggestions(false), 150)}
              onKeyDown={(e) => {
                if (!showSuggestions && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {
                  setShowSuggestions(true);
                  return;
                }
                if (e.key === 'ArrowDown') {
                  e.preventDefault();
                  setActiveIndex((i) => Math.min(i + 1, suggestions.length - 1));
                } else if (e.key === 'ArrowUp') {
                  e.preventDefault();
                  setActiveIndex((i) => Math.max(i - 1, 0));
                } else if (e.key === 'Enter') {
                  if (activeIndex >= 0 && suggestions[activeIndex]) {
                    setSelectedEmail(suggestions[activeIndex]);
                  }
                  setShowSuggestions(false);
                } else if (e.key === 'Escape') {
                  setShowSuggestions(false);
                }
              }}
            />
            {showSuggestions && (
              <ul id="email-suggestions" className="typeahead-dropdown" role="listbox">
                {suggestions.length === 0 && (
                  <li className="typeahead-empty">No matches</li>
                )}
                {suggestions.map((s, idx) => {
                  const q = (selectedEmail || '').toLowerCase();
                  const i = s.toLowerCase().indexOf(q);
                  const before = i >= 0 ? s.slice(0, i) : s;
                  const match  = i >= 0 ? s.slice(i, i + q.length) : '';
                  const after  = i >= 0 ? s.slice(i + q.length) : '';
                  return (
                    <li
                      key={s}
                      role="option"
                      aria-selected={idx === activeIndex}
                      className={`typeahead-item ${idx === activeIndex ? 'active' : ''}`}
                      onMouseDown={(e) => {
                        e.preventDefault();
                        setSelectedEmail(s);
                        setShowSuggestions(false);
                      }}
                      onMouseEnter={() => setActiveIndex(idx)}
                    >
                      {i >= 0 ? (<>{before}<mark className="typeahead-mark">{match}</mark>{after}</>) : s}
                    </li>
                  );
                })}
              </ul>
            )}
          </div>

          <button onClick={handleDeleteEmail}>Delete chosen E-Mail</button>
        </div>

        <div className="new-lecturer">
          <h2>Add new Lecturer</h2>
          <input
            type="text"
            placeholder="Full Name"
            value={fullName}
            onChange={e => setFullName(e.target.value)}
          />
          <input
            type="email"
            placeholder="New E-Mail"
            value={newEmail}
            onChange={e => setNewEmail(e.target.value)}
          />
          <button onClick={handleAddEmail}>Add Lecturer</button>
        </div>
      </div>

      {/* Shipping Provider and Item Count section */}
      <div className="shipping-container">
        <div className="item-count">
          <h4>Choose item count</h4>
          <select value={itemCount} onChange={e => setItemCount(e.target.value)}>
            {[...Array(10)].map((_, i) => (
              <option key={i + 1} value={i + 1}>{i + 1}</option>
            ))}
          </select>
        </div>
        <div className="shipping-provider">
          <h4>Shipping Provider</h4>
          <select value={shippingProvider} onChange={e => setShippingProvider(e.target.value)}>
            <option value="Royal Mail">Royal Mail</option>
            <option value="Amazon">Amazon</option>
            <option value="DPD">DPD</option>
            <option value="FedEx">FedEx</option>
            <option value="UPS">UPS</option>
            <option value="Evri">Evri</option>
            <option value="Other">Other</option>
          </select>
        </div>
      </div>

      {/* Additional Information */}
      <div className="additional-info-section">
        <h4>Additional Information</h4>
        <textarea
          placeholder="Type your custom information here..."
          value={providerDescription}
          onChange={e => setProviderDescription(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) { e.preventDefault(); handleSendEmailWithChosenEmail(); } }}
        />
        <button onClick={handleSendEmailWithChosenEmail}>
          Send Email/Log Information (Chosen Email)
        </button>
      </div>
    </div>
  );
};

export default EmailSelector;